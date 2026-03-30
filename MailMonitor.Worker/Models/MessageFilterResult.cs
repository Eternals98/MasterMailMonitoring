namespace MailMonitor.Worker.Models
{
    public sealed record MessageFilterResult(
        bool ShouldSkip,
        string Reason,
        string Subject,
        string ProcessingTag)
    {
        public static MessageFilterResult Continue(string subject, string processingTag)
            => new(false, string.Empty, subject, processingTag);

        public static MessageFilterResult Skip(string reason, string subject, string processingTag)
            => new(true, reason, subject, processingTag);
    }
}
