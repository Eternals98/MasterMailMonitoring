using MailMonitor.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMonitor.Domain.Entities.Jobs.Events
{
    public sealed record TriggerDeletedDomainEvent(Guid TriggerId) : IDomainEvent;
}
