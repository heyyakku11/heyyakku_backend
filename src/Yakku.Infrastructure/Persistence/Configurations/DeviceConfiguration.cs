using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class DeviceConfiguration : IEntityTypeConfiguration<Device>
    {
        public void Configure(EntityTypeBuilder<Device> builder)
        {
            builder.ToTable("devices", table =>
            {
                // An installation has at most one owner. (Neither is allowed: an orphan whose user was deleted.)
                table.HasCheckConstraint(
                    "CK_devices_single_owner",
                    "NOT (\"UserId\" IS NOT NULL AND \"GuestId\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_devices_push_token_hash_matches_token",
                    "(\"PushToken\" IS NULL) = (\"PushTokenHash\" IS NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.InstallationId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.PushToken)
                .HasColumnType("text");

            builder.Property(x => x.PushTokenHash)
                .HasMaxLength(64);

            builder.Property(x => x.Platform)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.DeviceModel)
                .HasMaxLength(150);

            builder.Property(x => x.OsVersion)
                .HasMaxLength(50);

            builder.Property(x => x.AppVersion)
                .HasMaxLength(50);

            builder.Property(x => x.AppBuild)
                .HasMaxLength(50);

            builder.Property(x => x.Locale)
                .HasMaxLength(20);

            builder.Property(x => x.Timezone)
                .HasMaxLength(100);

            builder.Property(x => x.NotificationPermission)
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_Devices_UserId");

            builder.HasIndex(x => x.GuestId)
                .HasFilter("\"GuestId\" IS NOT NULL")
                .HasDatabaseName("IX_Devices_GuestId");

            // A push token belongs to exactly one installation.
            builder.HasIndex(x => x.PushTokenHash)
                .IsUnique()
                .HasFilter("\"PushTokenHash\" IS NOT NULL")
                .HasDatabaseName("IX_Devices_PushTokenHash");

            builder.HasIndex(x => new { x.InstallationId, x.Platform })
                .IsUnique()
                .HasDatabaseName("IX_Devices_InstallationId_Platform");

            builder.HasOne(x => x.User)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // A guest's installations go with the guest when it is purged (they are transferred first on claim).
            builder.HasOne(x => x.Guest)
                .WithMany()
                .HasForeignKey(x => x.GuestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
