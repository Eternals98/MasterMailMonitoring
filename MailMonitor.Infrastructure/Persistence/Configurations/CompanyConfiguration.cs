using MailMonitor.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailMonitor.Infrastructure.Persistence.Configurations
{
    internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> builder)
        {
            builder.ToTable("Companies");

            builder.HasKey(company => company.Id);

            builder.Property(company => company.Id)
                .ValueGeneratedNever();

            builder.Property(company => company.Name)
                .IsRequired();

            builder.Property(company => company.Mail)
                .IsRequired();

            builder.Property(company => company.StartFrom)
                .IsRequired()
                .HasDefaultValue(string.Empty);

            var listComparer = new ValueComparer<List<string>>(
                (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
                values => (values ?? new List<string>())
                    .Aggregate(0, (hash, value) => HashCode.Combine(hash, (value ?? string.Empty).GetHashCode(StringComparison.Ordinal))),
                values => values == null ? new List<string>() : values.ToList());

            builder.Property(company => company.MailBox)
                .HasConversion(
                    values => ConfigurationDbContext.SerializeStringList(values),
                    serialized => ConfigurationDbContext.DeserializeStringList(serialized).ToList())
                .Metadata.SetValueComparer(listComparer);

            builder.Property(company => company.MailBox)
                .IsRequired()
                .HasDefaultValueSql("'[]'");

            builder.Property(company => company.FileTypes)
                .HasConversion(
                    values => ConfigurationDbContext.SerializeStringList(values),
                    serialized => ConfigurationDbContext.DeserializeStringList(serialized).ToList())
                .Metadata.SetValueComparer(listComparer);

            builder.Property(company => company.FileTypes)
                .IsRequired()
                .HasDefaultValueSql("'[]'");

            builder.Property(company => company.AttachmentKeywords)
                .HasConversion(
                    values => ConfigurationDbContext.SerializeStringList(values),
                    serialized => ConfigurationDbContext.DeserializeStringList(serialized).ToList())
                .Metadata.SetValueComparer(listComparer);

            builder.Property(company => company.AttachmentKeywords)
                .IsRequired()
                .HasDefaultValueSql("'[]'");

            builder.Property(company => company.StorageFolder)
                .IsRequired();

            builder.Property(company => company.ReportOutputFolder)
                .IsRequired()
                .HasDefaultValue(string.Empty);

            builder.Property(company => company.ProcessingTag)
                .IsRequired()
                .HasDefaultValue(Company.DefaultProcessingTag);

            builder.Property(company => company.OverrideGlobalProcessingTag)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(company => company.OverrideGlobalStorageFolder)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(company => company.OverrideGlobalReportOutputFolder)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(company => company.RecordType)
                .IsRequired()
                .HasDefaultValue(Company.RecordTypeSetting);

            builder.Property(company => company.ProcessedSubject)
                .IsRequired()
                .HasDefaultValue(string.Empty);

            builder.Property(company => company.ProcessedDate);

            builder.Property(company => company.ProcessedAttachmentsCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.HasIndex(company => company.RecordType);
            builder.HasIndex(company => company.Mail);
        }
    }
}
