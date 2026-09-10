using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
    {
        public void Configure(EntityTypeBuilder<NotificationPreference> builder)
        {
            builder.ToTable("NotificationPreferences");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.PushEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.PollActivityEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.OffersEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.AlertsEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.NormalEnabled)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_NotificationPreferences_UserId");
        }
    }
}
