using MailMonitor.Domain.Entities.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailMonitor.Infrastructure.Persistence.Configurations
{
    internal sealed class TriggerConfiguration : IEntityTypeConfiguration<Trigger>
    {
        public void Configure(EntityTypeBuilder<Trigger> builder)
        {
            builder.ToTable("Triggers");

            builder.HasKey(trigger => trigger.Id);

            builder.Property(trigger => trigger.Id)
                .ValueGeneratedNever();

            builder.Property(trigger => trigger.Name)
                .IsRequired();

            builder.Property(trigger => trigger.CronExpression)
                .IsRequired();
        }
    }
}
