using System.Globalization;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Application.Abstractions.Graph;
using MailMonitor.Application.Abstractions.Reporting;
using MailMonitor.Application.Abstractions.Storage;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Reporting;
using MailMonitor.Domain.Entities.Settings;
using Microsoft.Graph.Models;

namespace MailMonitor.Worker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly IGraphClient _graphClient;
        private readonly IAttachmentStorageService _attachmentStorageService;
        private readonly IReportingService _reportingService;
        private readonly int _loopDelaySeconds;

        public Worker(
            ILogger<Worker> logger,
            IConfigurationService configurationService,
            IGraphClient graphClient,
            IAttachmentStorageService attachmentStorageService,
            IReportingService reportingService,
            IConfiguration configuration)
        {
            _logger = logger;
            _configurationService = configurationService;
            _graphClient = graphClient;
            _attachmentStorageService = attachmentStorageService;
            _reportingService = reportingService;
            _loopDelaySeconds = Math.Max(configuration.GetValue<int?>("Processing:LoopDelaySeconds") ?? 60, 5);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Mail monitor worker started. Loop delay: {DelaySeconds} seconds.", _loopDelaySeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                var cycleStartedAt = DateTime.UtcNow;

                try
                {
                    await ProcessCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during mail monitoring cycle.");
                }

                var elapsed = DateTime.UtcNow - cycleStartedAt;
                var remainingDelay = TimeSpan.FromSeconds(_loopDelaySeconds) - elapsed;

                if (remainingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(remainingDelay, stoppingToken);
                }
            }
        }

        private async Task ProcessCycleAsync(CancellationToken cancellationToken)
        {
            var settings = await _configurationService.GetSettingsAsync();
            var validation = settings.Validate();

            if (validation.IsFailure)
            {
                _logger.LogWarning("Global settings are invalid: {ErrorCode} - {ErrorName}", validation.Error.Code, validation.Error.Name);
                return;
            }

            var companies = settings.CompanySettings
                .Where(company => company.RecordType == Company.RecordTypeSetting)
                .ToList();

            if (companies.Count == 0)
            {
                _logger.LogDebug("No companies configured for monitoring.");
                return;
            }

            foreach (var company in companies)
            {
                await ProcessCompanyAsync(company, settings, cancellationToken);
            }
        }

        private async Task ProcessCompanyAsync(Company company, Setting settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(company.Mail))
            {
                _logger.LogWarning("Skipping company {CompanyName}: mail account is empty.", company.Name);
                return;
            }

            var mailboxIds = company.MailBox
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (mailboxIds.Count == 0)
            {
                _logger.LogWarning("Skipping company {CompanyName}: no mailbox ids configured.", company.Name);
                return;
            }

            var startFromUtc = ParseStartFrom(company.StartFrom);

            foreach (var mailboxId in mailboxIds)
            {
                var messages = await _graphClient.GetMessagesAsync(company.Mail, mailboxId, startFromUtc, cancellationToken);

                foreach (var message in messages)
                {
                    await ProcessMessageAsync(company, settings, mailboxId, message, cancellationToken);
                }
            }
        }

        private async Task ProcessMessageAsync(
            Company company,
            Setting settings,
            string mailboxId,
            Message message,
            CancellationToken cancellationToken)
        {
            var messageId = message.Id;
            if (string.IsNullOrWhiteSpace(messageId))
            {
                return;
            }

            var subject = string.IsNullOrWhiteSpace(message.Subject)
                ? "(No Subject)"
                : message.Subject.Trim();

            var subjectKeywords = SplitValues(settings.MailSubjectKeywords);
            if (subjectKeywords.Count > 0 && !ContainsAny(subject, subjectKeywords))
            {
                LogStatistic(company, mailboxId, messageId, subject, false, 0, "Subject does not match global keywords", []);
                return;
            }

            if (message.HasAttachments != true)
            {
                LogStatistic(company, mailboxId, messageId, subject, false, 0, "Message has no attachments", []);
                return;
            }

            var attachments = await _graphClient.GetFileAttachmentsAsync(company.Mail, messageId, cancellationToken);
            var filteredAttachments = FilterAttachments(company, attachments).ToList();

            if (filteredAttachments.Count == 0)
            {
                LogStatistic(company, mailboxId, messageId, subject, false, 0, "No attachments matched configured filters", []);
                return;
            }

            var storedAttachmentPaths = new List<string>();

            foreach (var attachment in filteredAttachments)
            {
                var storageResult = _attachmentStorageService.StoreFile(company, subject, attachment, settings);
                if (storageResult.IsSuccess)
                {
                    storedAttachmentPaths.Add(storageResult.Value.FilePath);
                }
            }

            var processed = storedAttachmentPaths.Count > 0;

            if (processed)
            {
                var tag = string.IsNullOrWhiteSpace(company.ProcessingTag)
                    ? settings.ProcessingTag
                    : company.ProcessingTag;

                if (string.IsNullOrWhiteSpace(tag))
                {
                    tag = Company.DefaultProcessingTag;
                }

                var tagResult = await _graphClient.TagMessageAsync(company.Mail, messageId, tag, cancellationToken);
                if (tagResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Message {MessageId} processed but tag could not be applied. Error: {ErrorCode} - {ErrorName}",
                        messageId,
                        tagResult.Error.Code,
                        tagResult.Error.Name);
                }
            }

            var reason = processed ? string.Empty : "No attachments could be stored";
            LogStatistic(company, mailboxId, messageId, subject, processed, filteredAttachments.Count, reason, storedAttachmentPaths);
        }

        private IEnumerable<FileAttachment> FilterAttachments(Company company, IReadOnlyCollection<FileAttachment> attachments)
        {
            var allowedTypes = company.FileTypes
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().TrimStart('.'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var attachmentKeywords = company.AttachmentKeywords
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList();

            foreach (var attachment in attachments)
            {
                var attachmentName = attachment.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(attachmentName))
                {
                    continue;
                }

                if (allowedTypes.Count > 0)
                {
                    var extension = Path.GetExtension(attachmentName).TrimStart('.');
                    if (!allowedTypes.Contains(extension))
                    {
                        continue;
                    }
                }

                if (attachmentKeywords.Count > 0 && !ContainsAny(attachmentName, attachmentKeywords))
                {
                    continue;
                }

                yield return attachment;
            }
        }

        private static DateTimeOffset? ParseStartFrom(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : null;
        }

        private static IReadOnlyList<string> SplitValues(string? values)
        {
            if (string.IsNullOrWhiteSpace(values))
            {
                return [];
            }

            return values
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static bool ContainsAny(string source, IReadOnlyCollection<string> candidates)
        {
            return candidates.Any(candidate => source.Contains(candidate, StringComparison.OrdinalIgnoreCase));
        }

        private void LogStatistic(
            Company company,
            string mailboxId,
            string messageId,
            string subject,
            bool processed,
            int attachmentsCount,
            string reasonIgnored,
            IReadOnlyCollection<string> storedAttachments)
        {
            var statistic = new EmailProcessStatistic
            {
                Date = DateTime.UtcNow,
                CompanyName = company.Name,
                UserMail = company.Mail,
                Processed = processed,
                Subject = subject,
                AttachmentsCount = attachmentsCount,
                ReasonIgnored = reasonIgnored,
                Mailbox = mailboxId,
                StoredAttachments = storedAttachments,
                StorageFolder = company.StorageFolder,
                MessageId = messageId
            };

            _reportingService.LogEmailProcess(statistic);
        }
    }
}
