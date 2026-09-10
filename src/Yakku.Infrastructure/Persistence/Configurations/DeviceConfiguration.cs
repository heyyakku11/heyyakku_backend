using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class DeviceConfiguration : IEntityTypeConfiguration<Device>
    {
        public void Configure(EntityTypeBuilder<Device> builder)
        {
            builder.ToTable("Devices");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.InstallationId)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.PushToken)
                .HasColumnType("text");

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

            builder.HasIndex(x => x.InstallationId)
                .IsUnique()
                .HasDatabaseName("IX_Devices_InstallationId");

            builder.HasOne(x => x.User)
                .WithMany(x => x.Devices)
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
