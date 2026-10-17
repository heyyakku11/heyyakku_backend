using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(UserStatus.Active);

            // Every user has a plan; rows created without one (or before plans existed) are on the free plan.
            builder.Property(x => x.PlanId)
                .IsRequired()
                .HasDefaultValue(Plan.FreeId);

            builder.HasIndex(x => x.PlanId)
                .HasDatabaseName("IX_Users_PlanId");

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired();

            builder.HasIndex(x => x.PhoneNumber)
                .IsUnique()
                .HasDatabaseName("IX_Users_PhoneNumber");

            builder.HasOne(x => x.Profile)
                .WithOne(x => x.User)
                .HasForeignKey<UserProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.NotificationPreference)
                .WithOne(x => x.User)
                .HasForeignKey<NotificationPreference>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
