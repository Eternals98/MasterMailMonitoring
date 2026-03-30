using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Reporting;
using MailMonitor.Domain.Repositories;

namespace MailMonitor.Worker.Services
{
    public sealed class ProcessingLogService : IProcessingLogService
    {
        private readonly IEmailStatisticsRepository _emailStatisticsRepository;

        public ProcessingLogService(IEmailStatisticsRepository emailStatisticsRepository)
        {
            _emailStatisticsRepository = emailStatisticsRepository;
        }

        public void LogStatistic(
            Company company,
            string mailboxId,
            string? messageId,
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

            _emailStatisticsRepository.LogEmailProcess(statistic);
        }
    }
}
