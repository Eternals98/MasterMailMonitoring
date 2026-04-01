namespace MailMonitor.Api.Contracts.Companies;

public sealed record MailboxRecentMessagesResponse(
    DateTime CheckedAtUtc,
    bool Healthy,
    string UserMail,
    string MailboxId,
    IReadOnlyCollection<MailboxRecentMessageResponse> Messages,
    string ErrorCode,
    string ErrorMessage);
