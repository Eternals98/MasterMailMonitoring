using MailMonitor.Application.Abstractions.Reporting;
using MailMonitor.Domain.Entities.Reporting;
using MailMonitor.Domain.Repositories;
using MailMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MailMonitor.Infrastructure.Reporting
{
    public sealed class FileSystemStatisticsService : IReportingService, IEmailStatisticsRepository
    {
        private readonly ILogger<FileSystemStatisticsService> _logger;
        private readonly string _dbPath;

        public FileSystemStatisticsService(ILogger<FileSystemStatisticsService> logger, string dbPath)
        {
            _logger = logger;
            _dbPath = dbPath;
        }

        private ConfigurationDbContext CreateContext()
        {
            var context = new ConfigurationDbContext(_dbPath);
            context.Database.EnsureCreated();
            context.EnsureEmailStatisticsSchema();
            return context;
        }

        public void LogEmailProcess(EmailProcessStatistic statistic)
        {
            try
            {
                using var context = CreateContext();

                var storedAttachments = statistic.StoredAttachments?.ToArray() ?? Array.Empty<string>();
                statistic.StoredAttachments = storedAttachments;

                if (!string.IsNullOrWhiteSpace(statistic.MessageId))
                {
                    var existingEntry = context.EmailStatistics.FirstOrDefault(item => item.MessageId == statistic.MessageId);

                    if (existingEntry is not null)
                    {
                        existingEntry.CompanyName = statistic.CompanyName;
                        existingEntry.UserMail = statistic.UserMail;
                        existingEntry.Date = statistic.Date;
                        existingEntry.Processed = statistic.Processed;
                        existingEntry.Subject = statistic.Subject;
                        existingEntry.AttachmentsCount = statistic.AttachmentsCount;
                        existingEntry.ReasonIgnored = statistic.ReasonIgnored;
                        existingEntry.Mailbox = statistic.Mailbox;
                        existingEntry.StoredAttachments = storedAttachments;
                        existingEntry.StorageFolder = statistic.StorageFolder;
                        existingEntry.MessageId = statistic.MessageId;

                        context.EmailStatistics.Update(existingEntry);
                    }
                    else
                    {
                        context.EmailStatistics.Add(statistic);
                    }
                }
                else
                {
                    context.EmailStatistics.Add(statistic);
                }

                context.SaveChanges();

                _logger.LogInformation("Email processed logged: {Subject} for {UserMail}", statistic.Subject, statistic.UserMail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging email process statistic");
            }
        }

        public IEnumerable<ProcessedEmailReport> GetProcessedEmailsPerDay()
        {
            using var context = CreateContext();

            return context.EmailStatistics
                .Where(statistic => statistic.Processed)
                .GroupBy(statistic => statistic.Date.Date)
                .Select(group => new ProcessedEmailReport
                {
                    Date = group.Key,
                    TotalProcessed = group.Count()
                })
                .OrderBy(report => report.Date)
                .ToList();
        }

        public IEnumerable<IgnoredEmailReport> GetIgnoredEmailsPerDay()
        {
            using var context = CreateContext();

            return context.EmailStatistics
                .Where(statistic => !statistic.Processed)
                .GroupBy(statistic => new { statistic.Date.Date, statistic.ReasonIgnored })
                .Select(group => new IgnoredEmailReport
                {
                    Date = group.Key.Date,
                    Reason = group.Key.ReasonIgnored,
                    TotalIgnored = group.Count()
                })
                .OrderBy(report => report.Date)
                .ThenBy(report => report.Reason)
                .ToList();
        }

        public IEnumerable<ProcessedEmailPerCompanyReport> GetProcessedEmailsPerCompanyPerDay()
        {
            using var context = CreateContext();

            return context.EmailStatistics
                .Where(statistic => statistic.Processed)
                .GroupBy(statistic => new { statistic.Date.Date, statistic.CompanyName })
                .Select(group => new ProcessedEmailPerCompanyReport
                {
                    Date = group.Key.Date,
                    Company = group.Key.CompanyName,
                    TotalProcessed = group.Count()
                })
                .OrderBy(report => report.Date)
                .ThenBy(report => report.Company)
                .ToList();
        }

        public IEnumerable<EmailProcessStatistic> GetEmailStatistics()
        {
            using var context = CreateContext();

            return context.EmailStatistics
                .AsNoTracking()
                .OrderByDescending(statistic => statistic.Date)
                .ToList();
        }
    }
}


