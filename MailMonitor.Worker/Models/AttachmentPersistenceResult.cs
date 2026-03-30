namespace MailMonitor.Worker.Models
{
    public sealed record AttachmentPersistenceResult(
        IReadOnlyCollection<string> StoredAttachmentPaths,
        int FailedAttachmentsCount,
        string FailureReason);
}
