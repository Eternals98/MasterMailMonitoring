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

        public string BaseStorageFolder { get; set; } = string.Empty;
        public string MailSubjectKeywords { get; set; } = string.Empty;
        public string ProcessingTag { get; set; } = Company.DefaultProcessingTag;
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

            return Result.Success();
        }
    }
}
