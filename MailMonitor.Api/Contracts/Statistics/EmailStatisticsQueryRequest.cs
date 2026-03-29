namespace MailMonitor.Api.Contracts.Statistics;

public sealed class EmailStatisticsQueryRequest
{
    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public string? Company { get; init; }

    public bool? Processed { get; init; }
}
