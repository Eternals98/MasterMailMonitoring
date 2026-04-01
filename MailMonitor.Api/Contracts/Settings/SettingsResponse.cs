using System.Text.Json.Serialization;

namespace MailMonitor.Api.Contracts.Settings;

public sealed record SettingsResponse(
    [property: JsonPropertyName("baseStorageFolder")] string BaseStorageFolder,
    [property: JsonPropertyName("mailSubjectKeywords")] IReadOnlyCollection<string> MailSubjectKeywords,
    [property: JsonPropertyName("globalSearchKeywords")] IReadOnlyCollection<string> GlobalSearchKeywords,
    [property: JsonPropertyName("processingTag")] string ProcessingTag,
    [property: JsonPropertyName("defaultReportOutputFolder")] string DefaultReportOutputFolder,
    [property: JsonPropertyName("defaultFileTypes")] IReadOnlyCollection<string> DefaultFileTypes,
    [property: JsonPropertyName("defaultAttachmentKeywords")] IReadOnlyCollection<string> DefaultAttachmentKeywords,
    [property: JsonPropertyName("schedulerTimeZoneId")] string SchedulerTimeZoneId,
    [property: JsonPropertyName("schedulerFallbackCronExpression")] string SchedulerFallbackCronExpression,
    [property: JsonPropertyName("storageMaxRetries")] int StorageMaxRetries,
    [property: JsonPropertyName("storageBaseDelayMs")] int StorageBaseDelayMs,
    [property: JsonPropertyName("storageMaxDelayMs")] int StorageMaxDelayMs,
    [property: JsonPropertyName("graphHealthCheckEnabled")] bool GraphHealthCheckEnabled,
    [property: JsonPropertyName("mailboxSearchEnabled")] bool MailboxSearchEnabled,
    [property: JsonPropertyName("processingActionsEnabled")] bool ProcessingActionsEnabled,
    [property: JsonPropertyName("updatedAtUtc")] DateTime UpdatedAtUtc,
    [property: JsonPropertyName("updatedBy")] string UpdatedBy,
    [property: JsonPropertyName("revision")] int Revision);
