using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
    {
        public void Configure(EntityTypeBuilder<AdminSession> builder)
        {
            builder.ToTable("admin_sessions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AdminId).IsRequired();

            builder.Property(x => x.SessionHash).IsRequired().HasMaxLength(64);
            builder.Property(x => x.PreviousSessionHash).HasMaxLength(64);
            builder.Property(x => x.RevocationReason).HasMaxLength(64);

            builder.Property(x => x.IpAddress).HasColumnType("inet");
            builder.Property(x => x.UserAgent).HasMaxLength(512);

            builder.HasIndex(x => x.SessionHash)
                .IsUnique()
                .HasDatabaseName("IX_AdminSessions_SessionHash");

            builder.HasIndex(x => x.PreviousSessionHash)
                .HasDatabaseName("IX_AdminSessions_PreviousSessionHash");

            // "All live sessions of this admin" (logout everywhere, suspension) and expiry cleanup.
            builder.HasIndex(x => new { x.AdminId, x.RevokedAt })
                .HasDatabaseName("IX_AdminSessions_AdminId_RevokedAt");

            builder.HasIndex(x => x.AbsoluteExpiresAt)
                .HasDatabaseName("IX_AdminSessions_AbsoluteExpiresAt");

            builder.HasOne(x => x.Admin)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.AdminId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
