using MailMonitor.Application.Abstractions.Storage;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Worker.Models;
using Microsoft.Graph.Models;

namespace MailMonitor.Worker.Services
{
    public sealed class AttachmentPersistenceService : IAttachmentPersistenceService
    {
        private readonly IAttachmentStorageService _attachmentStorageService;
        private readonly ILogger<AttachmentPersistenceService> _logger;

        public AttachmentPersistenceService(
            IAttachmentStorageService attachmentStorageService,
            ILogger<AttachmentPersistenceService> logger)
        {
            _attachmentStorageService = attachmentStorageService;
            _logger = logger;
        }

        public AttachmentPersistenceResult StoreAttachments(
            Company company,
            Setting settings,
            string mailSubject,
            IReadOnlyCollection<FileAttachment> attachments)
        {
            var storedAttachmentPaths = new List<string>();
            var failedAttachments = 0;
            var failureMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var attachment in attachments)
            {
                using var attachmentScope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["attachmentName"] = attachment.Name ?? string.Empty
                });

                var storageResult = _attachmentStorageService.StoreFile(company, mailSubject, attachment, settings);
                if (storageResult.IsSuccess)
                {
                    storedAttachmentPaths.Add(storageResult.Value.FilePath);
                }
                else
                {
                    failedAttachments++;
                    failureMessages.Add($"{storageResult.Error.Code}: {storageResult.Error.Name}");
                    _logger.LogWarning(
                        "Attachment storage failed. Error: {ErrorCode} - {ErrorName}",
                        storageResult.Error.Code,
                        storageResult.Error.Name);
                }
            }

            return new AttachmentPersistenceResult(
                storedAttachmentPaths,
                failedAttachments,
                failureMessages.Count == 0
                    ? string.Empty
                    : string.Join(" | ", failureMessages));
        }
    }
}
