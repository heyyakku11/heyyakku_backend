using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class SystemLogConfiguration : IEntityTypeConfiguration<SystemLog>
    {
        public void Configure(EntityTypeBuilder<SystemLog> builder)
        {
            builder.ToTable("SystemLogs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Level)
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(x => x.EventType)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(x => x.Message)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.Details)
                .HasMaxLength(4000);

            builder.Property(x => x.Path)
                .HasMaxLength(256);

            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_SystemLogs_CreatedAt");
        }
    }
}
