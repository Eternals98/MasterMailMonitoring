using System.ComponentModel.DataAnnotations;

namespace MailMonitor.Api.Contracts.Companies;

public sealed class UpsertCompanyRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Mail { get; init; } = string.Empty;

    public string StartFrom { get; init; } = string.Empty;

    public IReadOnlyCollection<string> MailBox { get; init; } = [];

    public IReadOnlyCollection<string> FileTypes { get; init; } = [];

    public IReadOnlyCollection<string> AttachmentKeywords { get; init; } = [];

    [Required]
    public string StorageFolder { get; init; } = string.Empty;

    [Required]
    public string ReportOutputFolder { get; init; } = string.Empty;

    [Required]
    public string ProcessingTag { get; init; } = string.Empty;
}
