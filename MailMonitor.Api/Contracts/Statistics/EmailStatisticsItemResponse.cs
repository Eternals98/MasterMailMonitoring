namespace MailMonitor.Api.Contracts.Statistics;

public sealed record EmailStatisticsItemResponse(
    Guid Id,
    DateTime Date,
    string Company,
    string UserMail,
    bool Processed,
    string Subject,
    int AttachmentsCount,
    string ReasonIgnored,
    string Mailbox,
    string StorageFolder,
    IReadOnlyCollection<string> StoredAttachments,
    string? MessageId);
