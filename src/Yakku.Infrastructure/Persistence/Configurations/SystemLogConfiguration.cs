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

            builder.Property(x => x.EventType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Severity)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Message)
                .HasColumnType("text");

            builder.Property(x => x.IpAddress)
                .HasColumnType("inet");

            builder.Property(x => x.UserAgent)
                .HasColumnType("text");

            builder.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_SystemLogs_UserId");

            builder.HasIndex(x => x.GuestId)
                .HasDatabaseName("IX_SystemLogs_GuestId");

            builder.HasIndex(x => x.EventType)
                .HasDatabaseName("IX_SystemLogs_EventType");

            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_SystemLogs_CreatedAt");

            builder.HasOne(x => x.User)
                .WithMany(x => x.SystemLogs)
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Guest)
                .WithMany(x => x.SystemLogs)
                .HasForeignKey(x => x.GuestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
