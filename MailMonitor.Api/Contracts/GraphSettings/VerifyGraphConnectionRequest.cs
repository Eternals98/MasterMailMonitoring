namespace MailMonitor.Api.Contracts.GraphSettings;

public sealed record VerifyGraphConnectionRequest(
    string? UserMail,
    string? MailboxId);
