namespace MailMonitor.Api.Contracts.Companies;

public sealed record MailboxRecentMessageResponse(
    string MessageId,
    string Subject,
    DateTimeOffset? ReceivedDateTime,
    bool HasAttachments,
    string Sender);
