namespace MailMonitor.Api.Contracts.GraphSettings;

public sealed record GraphSettingsResponse(
    string Instance,
    string ClientId,
    string TenantId,
    string ClientSecretMasked,
    string GraphUserScopesJson,
    DateTime? LastVerificationAtUtc,
    bool? LastVerificationSucceeded,
    string LastVerificationErrorCode,
    string LastVerificationErrorMessage);
