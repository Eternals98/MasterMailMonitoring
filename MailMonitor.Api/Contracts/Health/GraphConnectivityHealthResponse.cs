namespace MailMonitor.Api.Contracts.Health;

public sealed record GraphConnectivityHealthResponse(
    DateTime CheckedAtUtc,
    bool Healthy,
    string UserMail,
    string MailboxId,
    string ErrorCode,
    string ErrorMessage);
