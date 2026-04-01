using System.Globalization;
using MailMonitor.Application.Abstractions.Configuration;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Worker.Models;
using MailMonitor.Worker.Services;
using Microsoft.Graph.Models;

namespace MailMonitor.Worker
{
    public sealed class Worker
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfigurationService _configurationService;
        private readonly IMailboxReader _mailboxReader;
        private readonly IMessageFilterService _messageFilterService;
        private readonly IAttachmentPersistenceService _attachmentPersistenceService;
        private readonly IMessageTaggingService _messageTaggingService;
        private readonly IProcessingLogService _processingLogService;

        public Worker(
            ILogger<Worker> logger,
            IConfigurationService configurationService,
            IMailboxReader mailboxReader,
            IMessageFilterService messageFilterService,
            IAttachmentPersistenceService attachmentPersistenceService,
            IMessageTaggingService messageTaggingService,
            IProcessingLogService processingLogService)
        {
            _logger = logger;
            _configurationService = configurationService;
            _mailboxReader = mailboxReader;
            _messageFilterService = messageFilterService;
            _attachmentPersistenceService = attachmentPersistenceService;
            _messageTaggingService = messageTaggingService;
            _processingLogService = processingLogService;
        }

        public async Task<ProcessingCycleMetrics> RunCycleAsync(CancellationToken cancellationToken)
        {
            var cycleMetrics = new ProcessingCycleMetrics();
            var cycleId = Guid.NewGuid().ToString("N");

            using var cycleScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["cycleId"] = cycleId
            });

            var settings = await _configurationService.GetSettingsAsync();
            var validation = settings.Validate();

            if (validation.IsFailure)
            {
                _logger.LogWarning(
                    "Global settings are invalid: {ErrorCode} - {ErrorName}",
                    validation.Error.Code,
                    validation.Error.Name);

                return cycleMetrics;
            }

            var companies = settings.CompanySettings
                .Where(company => company.RecordType == Company.RecordTypeSetting)
                .ToList();

            if (companies.Count == 0)
            {
                _logger.LogDebug("No companies configured for monitoring.");
                return cycleMetrics;
            }

            foreach (var company in companies)
            {
                await ProcessCompanyAsync(company, settings, cycleMetrics, cancellationToken);
            }

            _logger.LogInformation(
                "Mail processing cycle finished. Read: {Read}, Processed: {Processed}, Ignored: {Ignored}, Failed: {Failed}.",
                cycleMetrics.Read,
                cycleMetrics.Processed,
                cycleMetrics.Ignored,
                cycleMetrics.Failed);

            return cycleMetrics;
        }

        private async Task ProcessCompanyAsync(
            Company company,
            Setting settings,
            ProcessingCycleMetrics cycleMetrics,
            CancellationToken cancellationToken)
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
                using var mailboxScope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["company"] = company.Name,
                    ["mailbox"] = mailboxId
                });

                var messages = await _mailboxReader.GetMessagesAsync(
                    company.Mail,
                    mailboxId,
                    startFromUtc,
                    cancellationToken);

                foreach (var message in messages)
                {
                    cycleMetrics.IncrementRead();
                    try
                    {
                        await ProcessMessageAsync(company, settings, mailboxId, message, cycleMetrics, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        cycleMetrics.IncrementFailed();
                        _logger.LogError(ex, "Unexpected error while processing message.");
                    }
                }
            }
        }

        private async Task ProcessMessageAsync(
            Company company,
            Setting settings,
            string mailboxId,
            Message message,
            ProcessingCycleMetrics cycleMetrics,
            CancellationToken cancellationToken)
        {
            var messageId = message.Id?.Trim();
            using var messageScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["messageId"] = messageId ?? string.Empty,
                ["company"] = company.Name,
                ["mailbox"] = mailboxId
            });

            var filterResult = _messageFilterService.EvaluateMessage(
                company,
                settings,
                message,
                messageId);

            if (filterResult.ShouldSkip)
            {
                cycleMetrics.IncrementIgnored();
                _processingLogService.LogStatistic(
                    company,
                    mailboxId,
                    messageId,
                    filterResult.Subject,
                    false,
                    0,
                    filterResult.Reason,
                    []);

                return;
            }

            var attachments = await _mailboxReader.GetFileAttachmentsAsync(company.Mail, messageId!, cancellationToken);
            var filteredAttachments = _messageFilterService.FilterAttachments(company, attachments);

            if (filteredAttachments.Count == 0)
            {
                cycleMetrics.IncrementIgnored();
                _processingLogService.LogStatistic(
                    company,
                    mailboxId,
                    messageId,
                    filterResult.Subject,
                    false,
                    0,
                    "No attachments matched configured filters",
                    []);

                return;
            }

            if (!settings.ProcessingActionsEnabled)
            {
                cycleMetrics.IncrementIgnored();

                _processingLogService.LogStatistic(
                    company,
                    mailboxId,
                    messageId,
                    filterResult.Subject,
                    false,
                    filteredAttachments.Count,
                    "Processing actions disabled: evaluation only, attachment download and tagging skipped",
                    []);

                _logger.LogInformation(
                    "Processing actions are disabled. Skipping attachment storage and message tagging.");

                return;
            }

            var persistenceResult = _attachmentPersistenceService.StoreAttachments(
                company,
                settings,
                filterResult.Subject,
                filteredAttachments);

            if (persistenceResult.StoredAttachmentPaths.Count == 0)
            {
                cycleMetrics.IncrementFailed();
                _processingLogService.LogStatistic(
                    company,
                    mailboxId,
                    messageId,
                    filterResult.Subject,
                    false,
                    filteredAttachments.Count,
                    persistenceResult.FailureReason,
                    []);

                return;
            }

            if (persistenceResult.FailedAttachmentsCount > 0)
            {
                _logger.LogWarning(
                    "Stored {StoredCount}/{TotalCount} attachments. Partial failures: {FailureReason}",
                    persistenceResult.StoredAttachmentPaths.Count,
                    filteredAttachments.Count,
                    persistenceResult.FailureReason);
            }

            var tagResult = await _messageTaggingService.TagMessageAsync(
                company.Mail,
                messageId!,
                filterResult.ProcessingTag,
                cancellationToken);

            if (tagResult.IsFailure)
            {
                _logger.LogWarning(
                    "Message processed but tag could not be applied. Error: {ErrorCode} - {ErrorName}",
                    tagResult.Error.Code,
                    tagResult.Error.Name);
            }

            cycleMetrics.IncrementProcessed();

            _processingLogService.LogStatistic(
                company,
                mailboxId,
                messageId,
                filterResult.Subject,
                true,
                filteredAttachments.Count,
                string.Empty,
                persistenceResult.StoredAttachmentPaths);
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
    }
}
