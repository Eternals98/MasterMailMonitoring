using MailMonitor.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailMonitor.Infrastructure.Persistence.Configurations
{
    internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.ToTable("SystemSettings");

            builder.HasKey(setting => setting.Id);

            builder.Property(setting => setting.Id)
                .ValueGeneratedNever();

            builder.Property(setting => setting.BaseStorageFolder)
                .IsRequired();

            builder.Property(setting => setting.DefaultReportOutputFolder)
                .IsRequired();

            builder.Property(setting => setting.DefaultProcessingTag)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(setting => setting.MailSubjectKeywordsJson)
                .IsRequired();

            builder.Property(setting => setting.DefaultFileTypesJson)
                .IsRequired();

            builder.Property(setting => setting.DefaultAttachmentKeywordsJson)
                .IsRequired();

            builder.Property(setting => setting.SchedulerTimeZoneId)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(setting => setting.SchedulerFallbackCronExpression)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(setting => setting.StorageMaxRetries)
                .IsRequired();

            builder.Property(setting => setting.StorageBaseDelayMs)
                .IsRequired();

            builder.Property(setting => setting.StorageMaxDelayMs)
                .IsRequired();

            builder.Property(setting => setting.GraphHealthCheckEnabled)
                .IsRequired();

            builder.Property(setting => setting.MailboxSearchEnabled)
                .IsRequired();

            builder.Property(setting => setting.ProcessingActionsEnabled)
                .IsRequired();

            builder.Property(setting => setting.UpdatedAtUtc)
                .IsRequired();

            builder.Property(setting => setting.UpdatedBy)
                .HasMaxLength(200);

            builder.Property(setting => setting.Revision)
                .IsRequired();
        }
    }
}
