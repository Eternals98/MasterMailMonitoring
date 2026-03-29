using System.Text.Json.Serialization;

namespace MailMonitor.Api.Contracts.Settings;

public sealed record SettingsResponse(
    [property: JsonPropertyName("baseStorageFolder")] string BaseStorageFolder,
    [property: JsonPropertyName("mailSubjectKeywords")] IReadOnlyCollection<string> MailSubjectKeywords,
    [property: JsonPropertyName("processingTag")] string ProcessingTag);
