using MailMonitor.Domain.Entities.Reporting;

namespace MailMonitor.Application.Abstractions.Reporting
{
    public interface IReportingService
    {
        void LogEmailProcess(EmailProcessStatistic statistic);
        IEnumerable<ProcessedEmailReport> GetProcessedEmailsPerDay();
        IEnumerable<IgnoredEmailReport> GetIgnoredEmailsPerDay();
        IEnumerable<ProcessedEmailPerCompanyReport> GetProcessedEmailsPerCompanyPerDay();
        IEnumerable<EmailProcessStatistic> GetEmailStatistics();
    }

    public interface IEmailStatisticsExporter
    {
        Task ExportAsync(IEnumerable<EmailProcessStatistic> statistics, string outputPath, CancellationToken cancellationToken);
    }

    public class ProcessedEmailReport
    {
        public DateTime Date { get; set; }
        public int TotalProcessed { get; set; }
    }

    public class IgnoredEmailReport
    {
        public DateTime Date { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int TotalIgnored { get; set; }
    }

    public class ProcessedEmailPerCompanyReport
    {
        public DateTime Date { get; set; }
        public string Company { get; set; } = string.Empty;
        public int TotalProcessed { get; set; }
    }
}
