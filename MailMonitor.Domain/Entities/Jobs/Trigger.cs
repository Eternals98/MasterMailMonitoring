using MailMonitor.Domain.Abstractions;
using MailMonitor.Domain.Entities.Jobs.Events;

namespace MailMonitor.Domain.Entities.Jobs
{
    public sealed class Trigger : Entity
    {
        private Trigger(Guid id, string name, string cronExpression)
            : base(id)
        {
            Name = name;
            CronExpression = cronExpression;
        }

        public Trigger() : base(Guid.NewGuid())
        {
        }

        public string Name { get; set; } = string.Empty;
        public string CronExpression { get; set; } = string.Empty;

        public static Trigger Create(string name, string cronExpression, Dictionary<string, string> data)
        {
            return new Trigger(Guid.NewGuid(), name, cronExpression);
        }

        public static Result<Trigger> CreateValidated(string name, string cronExpression)
        {
            var validationResult = Validate(name, cronExpression);
            if (validationResult.IsFailure)
            {
                return Result.Failure<Trigger>(validationResult.Error);
            }

            var trigger = new Trigger(Guid.NewGuid(), name.Trim(), cronExpression.Trim());
            return Result.Success(trigger);
        }

        public Result Update(string name, string cronExpression, Dictionary<string, string> data)
        {
            var validationResult = Validate(name, cronExpression);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            Name = name.Trim();
            CronExpression = cronExpression.Trim();
            return Result.Success();
        }

        public Result Delete()
        {
            if (Id == Guid.Empty)
            {
                return Result.Failure(Error.NullValue);
            }

            RaiseDomainEvent(new TriggerDeletedDomainEvent(Id));
            return Result.Success();
        }

        private static Result Validate(string name, string cronExpression)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure(DomainErrors.Trigger.NameRequired);
            }

            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                return Result.Failure(DomainErrors.Trigger.CronExpressionRequired);
            }

            return Result.Success();
        }
    }
}
