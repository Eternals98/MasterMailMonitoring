namespace MailMonitor.Api.Contracts.Companies;

public sealed record CompanyDetailResponse(
    Guid Id,
    string Name,
    string Mail,
    string StartFrom,
    IReadOnlyCollection<string> MailBox,
    IReadOnlyCollection<string> FileTypes,
    IReadOnlyCollection<string> AttachmentKeywords,
    string StorageFolder,
    string ReportOutputFolder,
    string ProcessingTag,
    string RecordType,
    string ProcessedSubject,
    DateTime? ProcessedDate,
    int ProcessedAttachmentsCount);
