using System.ComponentModel.DataAnnotations;

namespace MailMonitor.Api.Contracts.Settings;

public sealed class UpdateSettingsRequest
{
    [Required]
    public string BaseStorageFolder { get; init; } = string.Empty;

    public IReadOnlyCollection<string>? MailSubjectKeywords { get; init; }
    public IReadOnlyCollection<string>? GlobalSearchKeywords { get; init; }
    public string? ProcessingTag { get; init; }
    public string? DefaultReportOutputFolder { get; init; }
    public IReadOnlyCollection<string>? DefaultFileTypes { get; init; }
    public IReadOnlyCollection<string>? DefaultAttachmentKeywords { get; init; }
    public string? SchedulerTimeZoneId { get; init; }
    public string? SchedulerFallbackCronExpression { get; init; }
    public int? StorageMaxRetries { get; init; }
    public int? StorageBaseDelayMs { get; init; }
    public int? StorageMaxDelayMs { get; init; }
    public bool? GraphHealthCheckEnabled { get; init; }
    public bool? MailboxSearchEnabled { get; init; }
    public bool? ProcessingActionsEnabled { get; init; }
    public string? UpdatedBy { get; init; }
}
