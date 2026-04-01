using MailMonitor.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailMonitor.Infrastructure.Persistence.Configurations
{
    internal sealed class GlobalSearchKeywordConfiguration : IEntityTypeConfiguration<GlobalSearchKeyword>
    {
        public void Configure(EntityTypeBuilder<GlobalSearchKeyword> builder)
        {
            builder.ToTable("GlobalSearchKeywords");

            builder.HasKey(item => item.Id);

            builder.Property(item => item.Id)
                .ValueGeneratedNever();

            builder.Property(item => item.Keyword)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(item => item.IsEnabled)
                .IsRequired();

            builder.Property(item => item.SortOrder)
                .IsRequired();

            builder.Property(item => item.CreatedAtUtc)
                .IsRequired();

            builder.Property(item => item.UpdatedAtUtc)
                .IsRequired();

            builder.HasIndex(item => item.Keyword)
                .IsUnique();
        }
    }
}
