using System.ComponentModel.DataAnnotations;

namespace MailMonitor.Api.Contracts.Settings;

public sealed class CheckStorageAccessRequest
{
    [Required]
    public string Path { get; init; } = string.Empty;
}
