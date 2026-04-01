using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies;
using MailMonitor.Domain.Entities.Jobs;

namespace MailMonitor.Domain.Entities.Settings
{
    public sealed class Setting
    {
        public const string KeyBaseStorageFolder = "BaseStorageFolder";
        public const string KeyMailSubjectKeywords = "MailSubjectKeywords";
        public const string KeyProcessingTag = "ProcessingTag";
        public const string DefaultSchedulerTimeZoneId = "America/New_York";
        public const string DefaultSchedulerFallbackCronExpression = "0 0/10 * ? * * *";

        public string BaseStorageFolder { get; set; } = string.Empty;
        public string MailSubjectKeywords { get; set; } = string.Empty;
        public string ProcessingTag { get; set; } = Company.DefaultProcessingTag;
        public string DefaultReportOutputFolder { get; set; } = @"\\Reports";
        public string DefaultFileTypes { get; set; } = string.Empty;
        public string DefaultAttachmentKeywords { get; set; } = string.Empty;
        public string SchedulerTimeZoneId { get; set; } = DefaultSchedulerTimeZoneId;
        public string SchedulerFallbackCronExpression { get; set; } = DefaultSchedulerFallbackCronExpression;
        public int StorageMaxRetries { get; set; } = 3;
        public int StorageBaseDelayMs { get; set; } = 300;
        public int StorageMaxDelayMs { get; set; } = 4000;
        public bool GraphHealthCheckEnabled { get; set; } = true;
        public bool MailboxSearchEnabled { get; set; } = true;
        public bool ProcessingActionsEnabled { get; set; } = true;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "system";
        public int Revision { get; set; } = 1;
        public IEnumerable<Company> CompanySettings { get; set; } = [];
        public IEnumerable<Trigger> TriggerSettings { get; set; } = [];

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(BaseStorageFolder))
            {
                return Result.Failure(DomainErrors.Setting.BaseStorageFolderRequired);
            }

            if (string.IsNullOrWhiteSpace(ProcessingTag))
            {
                return Result.Failure(DomainErrors.Setting.ProcessingTagRequired);
            }

            if (StorageMaxRetries < 0 || StorageMaxRetries > 10)
            {
                return Result.Failure(DomainErrors.Setting.InvalidStorageMaxRetries);
            }

            if (StorageBaseDelayMs < 0)
            {
                return Result.Failure(DomainErrors.Setting.InvalidStorageDelayRange);
            }

            if (StorageMaxDelayMs < StorageBaseDelayMs)
            {
                return Result.Failure(DomainErrors.Setting.InvalidStorageDelayRange);
            }

            return Result.Success();
        }
    }
}
