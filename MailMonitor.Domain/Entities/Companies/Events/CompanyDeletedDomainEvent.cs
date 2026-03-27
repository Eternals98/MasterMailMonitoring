using MailMonitor.Domain.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailMonitor.Domain.Entities.Companies.Events
{
    public sealed record CompanyDeletedDomainEvent(Guid CompanyId) : IDomainEvent;
}
