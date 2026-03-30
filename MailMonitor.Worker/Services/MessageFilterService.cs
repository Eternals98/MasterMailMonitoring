using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Domain.Repositories;
using MailMonitor.Worker.Models;
using Microsoft.Graph.Models;

namespace MailMonitor.Worker.Services
{
    public sealed class MessageFilterService : IMessageFilterService
    {
        private readonly IEmailStatisticsRepository _emailStatisticsRepository;

        public MessageFilterService(IEmailStatisticsRepository emailStatisticsRepository)
        {
            _emailStatisticsRepository = emailStatisticsRepository;
        }

        public MessageFilterResult EvaluateMessage(
            Company company,
            Setting settings,
            Message message,
            string? messageId)
        {
            var subject = string.IsNullOrWhiteSpace(message.Subject)
                ? "(No Subject)"
                : message.Subject.Trim();

            var processingTag = ResolveProcessingTag(company, settings);

            if (string.IsNullOrWhiteSpace(messageId))
            {
                return MessageFilterResult.Skip("Message has no MessageId.", subject, processingTag);
            }

            if (HasProcessingTag(message.Categories, processingTag))
            {
                return MessageFilterResult.Skip(
                    $"Message already contains processing tag '{processingTag}'.",
                    subject,
                    processingTag);
            }

            if (_emailStatisticsRepository.HasProcessedMessage(messageId))
            {
                return MessageFilterResult.Skip(
                    "Message was already processed previously (idempotency by MessageId).",
                    subject,
                    processingTag);
            }

            var subjectKeywords = SplitValues(settings.MailSubjectKeywords);
            if (subjectKeywords.Count > 0 && !ContainsAny(subject, subjectKeywords))
            {
                return MessageFilterResult.Skip("Subject does not match global keywords", subject, processingTag);
            }

            if (message.HasAttachments != true)
            {
                return MessageFilterResult.Skip("Message has no attachments", subject, processingTag);
            }

            return MessageFilterResult.Continue(subject, processingTag);
        }

        public IReadOnlyCollection<FileAttachment> FilterAttachments(
            Company company,
            IReadOnlyCollection<FileAttachment> attachments)
        {
            var allowedTypes = company.FileTypes
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().TrimStart('.'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var attachmentKeywords = company.AttachmentKeywords
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList();

            var filteredAttachments = new List<FileAttachment>();

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

                filteredAttachments.Add(attachment);
            }

            return filteredAttachments;
        }

        private static string ResolveProcessingTag(Company company, Setting settings)
        {
            var tag = string.IsNullOrWhiteSpace(company.ProcessingTag)
                ? settings.ProcessingTag
                : company.ProcessingTag;

            return string.IsNullOrWhiteSpace(tag)
                ? Company.DefaultProcessingTag
                : tag.Trim();
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

        private static bool HasProcessingTag(IReadOnlyList<string>? categories, string processingTag)
        {
            if (categories is null || categories.Count == 0 || string.IsNullOrWhiteSpace(processingTag))
            {
                return false;
            }

            return categories
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Any(value => string.Equals(value.Trim(), processingTag, StringComparison.OrdinalIgnoreCase));
        }
    }
}
