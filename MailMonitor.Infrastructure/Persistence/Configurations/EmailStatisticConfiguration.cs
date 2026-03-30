using MailMonitor.Domain.Entities.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailMonitor.Infrastructure.Persistence.Configurations
{
    internal sealed class EmailStatisticConfiguration : IEntityTypeConfiguration<EmailProcessStatistic>
    {
        public void Configure(EntityTypeBuilder<EmailProcessStatistic> builder)
        {
            builder.ToTable("EmailStatistics");

            builder.HasKey(statistic => statistic.Id);

            builder.Property(statistic => statistic.Id)
                .ValueGeneratedNever();

            builder.Property(statistic => statistic.Date)
                .IsRequired();

            builder.Property(statistic => statistic.CompanyName)
                .IsRequired();

            builder.Property(statistic => statistic.UserMail)
                .IsRequired();

            builder.Property(statistic => statistic.Subject)
                .IsRequired();

            builder.Property(statistic => statistic.Processed)
                .IsRequired();

            builder.Property(statistic => statistic.AttachmentsCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(statistic => statistic.ReasonIgnored)
                .IsRequired()
                .HasDefaultValue(string.Empty);

            builder.Property(statistic => statistic.Mailbox)
                .IsRequired()
                .HasDefaultValue(string.Empty);

            builder.Property(statistic => statistic.StorageFolder)
                .IsRequired()
                .HasDefaultValue(string.Empty);

            var comparer = new ValueComparer<IEnumerable<string>>(
                (left, right) => (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>()),
                values => (values ?? Array.Empty<string>())
                    .Aggregate(0, (hash, value) => HashCode.Combine(hash, (value ?? string.Empty).GetHashCode(StringComparison.Ordinal))),
                values => (values ?? Array.Empty<string>()).ToArray());

            builder.Property(statistic => statistic.StoredAttachments)
                .HasConversion(
                    values => ConfigurationDbContext.SerializeStringList(values),
                    serialized => ConfigurationDbContext.DeserializeStringList(serialized))
                .Metadata.SetValueComparer(comparer);

            builder.Property(statistic => statistic.StoredAttachments)
                .IsRequired()
                .HasDefaultValueSql("'[]'");

            builder.Property(statistic => statistic.MessageId);

            builder.HasIndex(statistic => statistic.MessageId);
            builder.HasIndex(statistic => statistic.Date);
            builder.HasIndex(statistic => statistic.CompanyName);
        }
    }
}
