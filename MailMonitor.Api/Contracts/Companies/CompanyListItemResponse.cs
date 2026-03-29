namespace MailMonitor.Api.Contracts.Companies;

public sealed record CompanyListItemResponse(
    Guid Id,
    string Name,
    string Mail,
    string StartFrom,
    string StorageFolder,
    string ReportOutputFolder,
    string ProcessingTag);
