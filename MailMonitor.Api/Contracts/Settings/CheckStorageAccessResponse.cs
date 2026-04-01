using System.Text.Json.Serialization;

namespace MailMonitor.Api.Contracts.Settings;

public sealed record CheckStorageAccessResponse(
    [property: JsonPropertyName("checkedAtUtc")] DateTime CheckedAtUtc,
    [property: JsonPropertyName("targetPath")] string TargetPath,
    [property: JsonPropertyName("normalizedPath")] string NormalizedPath,
    [property: JsonPropertyName("exists")] bool Exists,
    [property: JsonPropertyName("canRead")] bool CanRead,
    [property: JsonPropertyName("canWrite")] bool CanWrite,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message);
