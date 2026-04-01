using System.ComponentModel.DataAnnotations;

namespace MailMonitor.Api.Contracts.Companies;

public sealed class ResolveMailboxesRequest
{
    [Required]
    public string UserMail { get; init; } = string.Empty;

    public IReadOnlyCollection<string> MailboxIds { get; init; } = [];
}
