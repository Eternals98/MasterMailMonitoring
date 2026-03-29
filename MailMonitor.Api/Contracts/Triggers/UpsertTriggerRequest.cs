using System.ComponentModel.DataAnnotations;

namespace MailMonitor.Api.Contracts.Triggers;

public sealed class UpsertTriggerRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string CronExpression { get; init; } = string.Empty;
}
