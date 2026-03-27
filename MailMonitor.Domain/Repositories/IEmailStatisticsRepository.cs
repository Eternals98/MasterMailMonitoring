using MailMonitor.Domain.Entities.Reporting;

namespace MailMonitor.Domain.Repositories
{
    public interface IEmailStatisticsRepository
    {
        void LogEmailProcess(EmailProcessStatistic statistic);
        IEnumerable<EmailProcessStatistic> GetEmailStatistics();
    }
}
