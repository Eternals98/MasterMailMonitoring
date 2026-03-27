using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Companies.Events;

namespace MailMonitor.Domain.Entities.Companies
{
    public sealed class Company : Entity
    {
        public const string RecordTypeSetting = nameof(RecordTypeSetting);
        public const string RecordTypeProcessedEmail = nameof(RecordTypeProcessedEmail);
        public const string DefaultProcessingTag = "OnBase";

        private Company(
            Guid id,
            string name,
            string mail,
            string startFrom,
            IEnumerable<string>? mailBox,
            IEnumerable<string>? fileTypes,
            IEnumerable<string>? attachmentKeywords,
            string storageFolder,
            string reportOutputFolder,
            string processingTag)
            : base(id)
        {
            Name = name;
            Mail = mail;
            StartFrom = startFrom;
            MailBox = CleanList(mailBox);
            FileTypes = CleanList(fileTypes);
            AttachmentKeywords = CleanList(attachmentKeywords);
            StorageFolder = storageFolder;
            ReportOutputFolder = reportOutputFolder;
            ProcessingTag = processingTag;
        }

        public Company() : base(Guid.NewGuid())
        {
        }

        public string Name { get; set; } = string.Empty;
        public string Mail { get; set; } = string.Empty;
        public string StartFrom { get; set; } = string.Empty;
        public List<string> MailBox { get; set; } = [];
        public List<string> FileTypes { get; set; } = [];
        public List<string> AttachmentKeywords { get; set; } = [];
        public string StorageFolder { get; set; } = string.Empty;
        public string ReportOutputFolder { get; set; } = string.Empty;
        public string ProcessingTag { get; set; } = DefaultProcessingTag;

        // Legacy persistence flags kept for backward compatibility with the existing DB schema.
        public string RecordType { get; set; } = RecordTypeSetting;
        public string ProcessedSubject { get; set; } = string.Empty;
        public DateTime? ProcessedDate { get; set; }
        public int ProcessedAttachmentsCount { get; set; }

        public static Company Create(
            string name,
            string mail,
            string startFrom,
            List<string> mailBox,
            List<string> fileTypes,
            string storageFolder,
            string reportOutputFolder)
        {
            return new Company(
                Guid.NewGuid(),
                name,
                mail,
                startFrom,
                mailBox,
                fileTypes,
                null,
                storageFolder,
                reportOutputFolder,
                DefaultProcessingTag);
        }

        public static Result<Company> CreateValidated(
            string name,
            string mail,
            string startFrom,
            IEnumerable<string>? mailBox,
            IEnumerable<string>? fileTypes,
            IEnumerable<string>? attachmentKeywords,
            string storageFolder,
            string reportOutputFolder,
            string processingTag = DefaultProcessingTag)
        {
            var validationResult = ValidateData(name, mail, startFrom, storageFolder, reportOutputFolder, processingTag);
            if (validationResult.IsFailure)
            {
                return Result.Failure<Company>(validationResult.Error);
            }

            var company = new Company(
                Guid.NewGuid(),
                name.Trim(),
                mail.Trim(),
                startFrom.Trim(),
                mailBox,
                fileTypes,
                attachmentKeywords,
                storageFolder.Trim(),
                reportOutputFolder.Trim(),
                processingTag.Trim());

            return Result.Success(company);
        }

        public Result Update(
            string name,
            string mail,
            string startFrom,
            List<string> mailBox,
            List<string> fileTypes,
            string storageFolder,
            string reportOutputFolder)
        {
            return Update(
                name,
                mail,
                startFrom,
                mailBox,
                fileTypes,
                AttachmentKeywords,
                storageFolder,
                reportOutputFolder,
                ProcessingTag);
        }

        public Result Update(
            string name,
            string mail,
            string startFrom,
            IEnumerable<string>? mailBox,
            IEnumerable<string>? fileTypes,
            IEnumerable<string>? attachmentKeywords,
            string storageFolder,
            string reportOutputFolder,
            string processingTag)
        {
            var validationResult = ValidateData(name, mail, startFrom, storageFolder, reportOutputFolder, processingTag);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            Name = name.Trim();
            Mail = mail.Trim();
            StartFrom = startFrom.Trim();
            MailBox = CleanList(mailBox);
            FileTypes = CleanList(fileTypes);
            AttachmentKeywords = CleanList(attachmentKeywords);
            StorageFolder = storageFolder.Trim();
            ReportOutputFolder = reportOutputFolder.Trim();
            ProcessingTag = processingTag.Trim();

            return Result.Success();
        }

        public Result ConfigureProcessing(string processingTag, IEnumerable<string>? attachmentKeywords)
        {
            if (string.IsNullOrWhiteSpace(processingTag))
            {
                return Result.Failure(DomainErrors.Company.ProcessingTagRequired);
            }

            ProcessingTag = processingTag.Trim();
            AttachmentKeywords = CleanList(attachmentKeywords);
            return Result.Success();
        }

        public Result RegisterProcessedEmail(string subject, DateTime processedDateUtc, int processedAttachmentsCount)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Result.Failure(DomainErrors.Company.ProcessedSubjectRequired);
            }

            if (processedAttachmentsCount < 0)
            {
                return Result.Failure(DomainErrors.Company.InvalidProcessedAttachmentCount);
            }

            RecordType = RecordTypeProcessedEmail;
            ProcessedSubject = subject.Trim();
            ProcessedDate = processedDateUtc;
            ProcessedAttachmentsCount = processedAttachmentsCount;

            return Result.Success();
        }

        public void ResetProcessedState()
        {
            RecordType = RecordTypeSetting;
            ProcessedSubject = string.Empty;
            ProcessedDate = null;
            ProcessedAttachmentsCount = 0;
        }

        public Result Delete()
        {
            if (Id == Guid.Empty)
            {
                return Result.Failure(Error.NullValue);
            }

            RaiseDomainEvent(new CompanyDeletedDomainEvent(Id));
            return Result.Success();
        }

        private static List<string> CleanList(IEnumerable<string>? values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }

        private static Result ValidateData(
            string name,
            string mail,
            string startFrom,
            string storageFolder,
            string reportOutputFolder,
            string processingTag)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure(DomainErrors.Company.NameRequired);
            }

            if (string.IsNullOrWhiteSpace(mail))
            {
                return Result.Failure(DomainErrors.Company.MailRequired);
            }

            if (string.IsNullOrWhiteSpace(storageFolder))
            {
                return Result.Failure(DomainErrors.Company.StorageFolderRequired);
            }

            if (string.IsNullOrWhiteSpace(reportOutputFolder))
            {
                return Result.Failure(DomainErrors.Company.ReportOutputFolderRequired);
            }

            if (string.IsNullOrWhiteSpace(processingTag))
            {
                return Result.Failure(DomainErrors.Company.ProcessingTagRequired);
            }

            if (!string.IsNullOrWhiteSpace(startFrom) &&
                !DateTimeOffset.TryParse(startFrom, out _))
            {
                return Result.Failure(DomainErrors.Company.InvalidStartFrom);
            }

            return Result.Success();
        }
    }
}
