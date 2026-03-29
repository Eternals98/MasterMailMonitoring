namespace MailMonitor.Api.Contracts.Triggers;

public sealed record TriggerResponse(
    Guid Id,
    string Name,
    string CronExpression);
