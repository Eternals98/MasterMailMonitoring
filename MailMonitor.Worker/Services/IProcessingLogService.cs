using MailMonitor.Domain.Entities.Companies;

namespace MailMonitor.Worker.Services
{
    public interface IProcessingLogService
    {
        void LogStatistic(
            Company company,
            string mailboxId,
            string? messageId,
            string subject,
            bool processed,
            int attachmentsCount,
            string reasonIgnored,
            IReadOnlyCollection<string> storedAttachments);
    }
}
