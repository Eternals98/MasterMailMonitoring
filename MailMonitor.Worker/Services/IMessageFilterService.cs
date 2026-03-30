using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Settings;
using MailMonitor.Worker.Models;
using Microsoft.Graph.Models;

namespace MailMonitor.Worker.Services
{
    public interface IMessageFilterService
    {
        MessageFilterResult EvaluateMessage(
            Company company,
            Setting settings,
            Message message,
            string? messageId);

        IReadOnlyCollection<FileAttachment> FilterAttachments(
            Company company,
            IReadOnlyCollection<FileAttachment> attachments);
    }
}
