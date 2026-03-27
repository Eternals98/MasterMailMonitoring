using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Jobs.Events;

namespace MailMonitor.Domain.Entities.Jobs
{
    public sealed class Job : Entity
    {
        private Job(Guid id, string name, string type, string description, List<Trigger> triggers)
            : base(id)
        {
            Name = name;
            Type = type;
            Description = description;
            Triggers = triggers;
        }

        public Job() : base(Guid.NewGuid())
        {
        }

        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Trigger> Triggers { get; set; } = [];

        public static Job Create(string name, string type, string description, List<Trigger> triggers)
        {
            return new Job(Guid.NewGuid(), name, type ?? string.Empty, description, triggers ?? []);
        }

        public static Result<Job> CreateValidated(string name, string type, string description, IEnumerable<Trigger>? triggers)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure<Job>(DomainErrors.Job.NameRequired);
            }

            var job = new Job(
                Guid.NewGuid(),
                name.Trim(),
                type?.Trim() ?? string.Empty,
                description?.Trim() ?? string.Empty,
                triggers?.ToList() ?? []);

            return Result.Success(job);
        }

        public Result Update(string name, string type, string description, List<Trigger> triggers)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure(DomainErrors.Job.NameRequired);
            }

            Name = name.Trim();
            Type = type?.Trim() ?? string.Empty;
            Description = description?.Trim() ?? string.Empty;
            Triggers = triggers ?? [];

            return Result.Success();
        }

        public Result Delete()
        {
            if (Id == Guid.Empty)
            {
                return Result.Failure(Error.NullValue);
            }

            RaiseDomainEvent(new JobDeletedDomainEvent(Id));
            return Result.Success();
        }
    }
}
