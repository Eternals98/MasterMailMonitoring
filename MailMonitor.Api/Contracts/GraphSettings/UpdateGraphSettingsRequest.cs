using System.ComponentModel.DataAnnotations;

namespace MailMonitor.Api.Contracts.GraphSettings;

public sealed class UpdateGraphSettingsRequest
{
    [Required]
    public string Instance { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    [Required]
    public string GraphUserScopesJson { get; init; } = "[]";
}
