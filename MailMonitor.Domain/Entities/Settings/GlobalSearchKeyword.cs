namespace MailMonitor.Domain.Entities.Settings
{
    public sealed class GlobalSearchKeyword
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Keyword { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
