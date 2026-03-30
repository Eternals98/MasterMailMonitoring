namespace MailMonitor.Infrastructure.Storage
{
    public sealed class AttachmentStorageOptions
    {
        public const string SectionName = "Storage";

        public int MaxRetries { get; set; } = 3;
        public int BaseDelayMilliseconds { get; set; } = 300;
        public int MaxDelayMilliseconds { get; set; } = 4000;
    }
}
