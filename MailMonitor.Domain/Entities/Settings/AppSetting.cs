using MailMonitor.Domain.Abstractions;

namespace MailMonitor.Domain.Entities.Settings
{
    public class AppSetting
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                return Result.Failure(DomainErrors.AppSetting.KeyRequired);
            }

            if (string.IsNullOrWhiteSpace(Value))
            {
                return Result.Failure(DomainErrors.AppSetting.ValueRequired);
            }

            return Result.Success();
        }
    }
}
