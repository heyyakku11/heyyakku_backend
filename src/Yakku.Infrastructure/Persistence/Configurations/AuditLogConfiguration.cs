using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AdminId)
                .IsRequired();

            builder.Property(x => x.Action)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.ResourceType)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.ResourceId)
                .IsRequired();

            builder.Property(x => x.ResourceName)
                .HasMaxLength(500);

            builder.Property(x => x.OldValue)
                .HasColumnType("text");

            builder.Property(x => x.NewValue)
                .HasColumnType("text");

            builder.Property(x => x.IpAddress)
                .HasColumnType("inet");

            builder.Property(x => x.UserAgent)
                .HasColumnType("text");

            builder.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            builder.HasIndex(x => x.AdminId)
                .HasDatabaseName("IX_AuditLogs_AdminId");

            builder.HasIndex(x => new { x.ResourceType, x.ResourceId })
                .HasDatabaseName("IX_AuditLogs_ResourceType_ResourceId");

            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_AuditLogs_CreatedAt");

            builder.HasOne(x => x.Admin)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.AdminId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
