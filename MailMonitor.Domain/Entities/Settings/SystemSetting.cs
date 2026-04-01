using MailMonitor.Domain.Entities.Companies;

namespace MailMonitor.Domain.Entities.Settings
{
    public sealed class SystemSetting
    {
        public const int SingletonId = 1;

        public int Id { get; set; } = SingletonId;
        public string BaseStorageFolder { get; set; } = @"C:\\OnBase";
        public string DefaultReportOutputFolder { get; set; } = @"\\Reports";
        public string DefaultProcessingTag { get; set; } = Company.DefaultProcessingTag;
        public string MailSubjectKeywordsJson { get; set; } = "[]";
        public string DefaultFileTypesJson { get; set; } = "[]";
        public string DefaultAttachmentKeywordsJson { get; set; } = "[]";
        public string SchedulerTimeZoneId { get; set; } = "America/New_York";
        public string SchedulerFallbackCronExpression { get; set; } = "0 0/10 * ? * * *";
        public int StorageMaxRetries { get; set; } = 3;
        public int StorageBaseDelayMs { get; set; } = 300;
        public int StorageMaxDelayMs { get; set; } = 4000;
        public bool GraphHealthCheckEnabled { get; set; } = true;
        public bool MailboxSearchEnabled { get; set; } = true;
        public bool ProcessingActionsEnabled { get; set; } = true;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
        public int Revision { get; set; } = 1;
    }
}
