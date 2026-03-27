using MailMonitor.Domain.Abstractions;

namespace MailMonitor.Domain.Entities.Jobs.Events
{
    public sealed record JobDeletedDomainEvent(Guid JobId) : IDomainEvent;
}