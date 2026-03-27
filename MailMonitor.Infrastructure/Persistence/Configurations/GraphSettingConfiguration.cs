using MailMonitor.Domain.Entities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailMonitor.Infrastructure.Persistence.Configurations
{
    internal sealed class GraphSettingConfiguration : IEntityTypeConfiguration<GraphSetting>
    {
        public void Configure(EntityTypeBuilder<GraphSetting> builder)
        {
            builder.ToTable("GraphSettings");

            builder.HasKey(graphSetting => graphSetting.Id);

            builder.Property(graphSetting => graphSetting.Id)
                .ValueGeneratedOnAdd();

            builder.Property(graphSetting => graphSetting.Instance)
                .IsRequired();

            builder.Property(graphSetting => graphSetting.ClientId)
                .IsRequired();

            builder.Property(graphSetting => graphSetting.TenantId)
                .IsRequired();

            builder.Property(graphSetting => graphSetting.ClientSecret)
                .IsRequired();

            builder.Property(graphSetting => graphSetting.GraphUserScopesJson)
                .IsRequired()
                .HasDefaultValue("[]");
        }
    }
}
