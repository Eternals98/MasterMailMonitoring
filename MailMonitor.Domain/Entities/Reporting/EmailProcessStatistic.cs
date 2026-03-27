using MailMonitor.Domain.Abstractions;

namespace MailMonitor.Domain.Entities.Reporting
{
    public sealed class EmailProcessStatistic
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string CompanyName { get; set; } = string.Empty;
        public string UserMail { get; set; } = string.Empty;
        public bool Processed { get; set; }
        public string Subject { get; set; } = string.Empty;
        public int AttachmentsCount { get; set; }
        public string ReasonIgnored { get; set; } = string.Empty;
        public string Mailbox { get; set; } = string.Empty;
        public IEnumerable<string> StoredAttachments { get; set; } = Array.Empty<string>();
        public string StorageFolder { get; set; } = string.Empty;
        public string? MessageId { get; set; }

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                return Result.Failure(DomainErrors.EmailProcessStatistic.CompanyNameRequired);
            }

            if (string.IsNullOrWhiteSpace(UserMail))
            {
                return Result.Failure(DomainErrors.EmailProcessStatistic.UserMailRequired);
            }

            if (string.IsNullOrWhiteSpace(Subject))
            {
                return Result.Failure(DomainErrors.EmailProcessStatistic.SubjectRequired);
            }

            if (AttachmentsCount < 0)
            {
                return Result.Failure(DomainErrors.EmailProcessStatistic.InvalidAttachmentsCount);
            }

            return Result.Success();
        }
    }
}
