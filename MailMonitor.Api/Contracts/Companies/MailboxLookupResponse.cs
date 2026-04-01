namespace MailMonitor.Api.Contracts.Companies;

public sealed record MailboxLookupResponse(
    string Id,
    string DisplayName,
    string Path);
