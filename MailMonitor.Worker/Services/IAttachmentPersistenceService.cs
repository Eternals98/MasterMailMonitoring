using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Worker.Models;
using Microsoft.Graph.Models;

namespace MailMonitor.Worker.Services
{
    public interface IAttachmentPersistenceService
    {
        AttachmentPersistenceResult StoreAttachments(
            Company company,
            Setting settings,
            string mailSubject,
            IReadOnlyCollection<FileAttachment> attachments);
    }
}
