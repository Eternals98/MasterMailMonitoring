namespace MailMonitor.Domain.Abstractions
{
    public static class DomainErrors
    {
        public static class Company
        {
            public static readonly Error NameRequired = new("Company.NameRequired", "Company name is required.");
            public static readonly Error MailRequired = new("Company.MailRequired", "Company mail is required.");
            public static readonly Error StorageFolderRequired = new("Company.StorageFolderRequired", "Storage folder is required.");
            public static readonly Error StorageFolderMustBeRelativeWhenUsingGlobal = new("Company.StorageFolderMustBeRelativeWhenUsingGlobal", "Storage folder must be relative when global storage is enabled.");
            public static readonly Error StorageFolderMustBeAbsoluteWhenOverridingGlobal = new("Company.StorageFolderMustBeAbsoluteWhenOverridingGlobal", "Storage folder must be absolute when overriding global storage.");
            public static readonly Error ReportOutputFolderRequired = new("Company.ReportOutputFolderRequired", "Report output folder is required.");
            public static readonly Error ReportOutputFolderMustBeRelativeWhenUsingGlobal = new("Company.ReportOutputFolderMustBeRelativeWhenUsingGlobal", "Report output folder must be relative when global report output is enabled.");
            public static readonly Error ReportOutputFolderMustBeAbsoluteWhenOverridingGlobal = new("Company.ReportOutputFolderMustBeAbsoluteWhenOverridingGlobal", "Report output folder must be absolute when overriding global report output.");
            public static readonly Error InvalidStartFrom = new("Company.InvalidStartFrom", "StartFrom must be a valid date in ISO format or a parseable date value.");
            public static readonly Error ProcessingTagRequired = new("Company.ProcessingTagRequired", "Processing tag is required.");
            public static readonly Error ProcessedSubjectRequired = new("Company.ProcessedSubjectRequired", "Processed subject is required.");
            public static readonly Error InvalidProcessedAttachmentCount = new("Company.InvalidProcessedAttachmentCount", "Processed attachments count cannot be negative.");
        }

        public static class Trigger
        {
            public static readonly Error NameRequired = new("Trigger.NameRequired", "Trigger name is required.");
            public static readonly Error CronExpressionRequired = new("Trigger.CronExpressionRequired", "Trigger cron expression is required.");
        }

        public static class Job
        {
            public static readonly Error NameRequired = new("Job.NameRequired", "Job name is required.");
        }

        public static class GraphSetting
        {
            public static readonly Error InstanceRequired = new("GraphSetting.InstanceRequired", "Graph instance URL is required.");
            public static readonly Error ClientIdRequired = new("GraphSetting.ClientIdRequired", "Graph client id is required.");
            public static readonly Error TenantIdRequired = new("GraphSetting.TenantIdRequired", "Graph tenant id is required.");
            public static readonly Error ClientSecretRequired = new("GraphSetting.ClientSecretRequired", "Graph client secret is required.");
            public static readonly Error InvalidScopesJson = new("GraphSetting.InvalidScopesJson", "Graph scopes must be valid JSON array.");
        }

        public static class AppSetting
        {
            public static readonly Error KeyRequired = new("AppSetting.KeyRequired", "App setting key is required.");
            public static readonly Error ValueRequired = new("AppSetting.ValueRequired", "App setting value is required.");
        }

        public static class Setting
        {
            public static readonly Error BaseStorageFolderRequired = new("Setting.BaseStorageFolderRequired", "Base storage folder is required.");
            public static readonly Error ProcessingTagRequired = new("Setting.ProcessingTagRequired", "Processing tag is required.");
            public static readonly Error InvalidStorageMaxRetries = new("Setting.InvalidStorageMaxRetries", "Storage max retries must be between 0 and 10.");
            public static readonly Error InvalidStorageDelayRange = new("Setting.InvalidStorageDelayRange", "Storage delay values are invalid. Max delay must be greater than or equal to base delay.");
        }

        public static class EmailProcessStatistic
        {
            public static readonly Error CompanyNameRequired = new("EmailProcessStatistic.CompanyNameRequired", "Company name is required.");
            public static readonly Error UserMailRequired = new("EmailProcessStatistic.UserMailRequired", "User mail is required.");
            public static readonly Error SubjectRequired = new("EmailProcessStatistic.SubjectRequired", "Email subject is required.");
            public static readonly Error InvalidAttachmentsCount = new("EmailProcessStatistic.InvalidAttachmentsCount", "Attachments count cannot be negative.");
        }

        public static class Storage
        {
            public static readonly Error InvalidBasePath = new("Storage.InvalidBasePath", "Base storage path is invalid or empty.");
            public static readonly Error InvalidRelativePath = new("Storage.InvalidRelativePath", "Storage relative path contains invalid values.");
            public static readonly Error PathTraversalDetected = new("Storage.PathTraversalDetected", "Path traversal attempt detected while building storage path.");
            public static readonly Error PermissionDenied = new("Storage.PermissionDenied", "Permission denied while writing attachment to storage.");
            public static readonly Error PathUnavailable = new("Storage.PathUnavailable", "Storage path is unavailable or not reachable.");
            public static readonly Error TransientNetworkFailure = new("Storage.TransientNetworkFailure", "Transient network/UNC failure while writing attachment.");
        }
    }
}
