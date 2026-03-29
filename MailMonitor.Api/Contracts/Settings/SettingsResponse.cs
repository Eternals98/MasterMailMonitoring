namespace MailMonitor.Api.Contracts.Settings;

public sealed record SettingsResponse(
    string BaseStorageFolder,
    IReadOnlyCollection<string> MailSubjectKeywords,
    string ProcessingTag);
