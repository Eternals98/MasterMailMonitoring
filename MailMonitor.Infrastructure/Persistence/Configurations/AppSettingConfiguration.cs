using MailMonitor.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailMonitor.Infrastructure.Persistence.Configurations
{
    internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
    {
        public void Configure(EntityTypeBuilder<AppSetting> builder)
        {
            builder.ToTable("AppSettings");

            builder.HasKey(setting => setting.Key);

            builder.Property(setting => setting.Key)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(setting => setting.Value)
                .IsRequired();

            builder.Property(setting => setting.Description);

            builder.Property(setting => setting.ExpiresAt);
        }
    }
}
