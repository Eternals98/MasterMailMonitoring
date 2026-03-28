using System.ComponentModel.DataAnnotations;

namespace MailMonitor.Api.Contracts.Settings;

public sealed class UpdateSettingsRequest
{
    [Required]
    public string BaseStorageFolder { get; init; } = string.Empty;
}
