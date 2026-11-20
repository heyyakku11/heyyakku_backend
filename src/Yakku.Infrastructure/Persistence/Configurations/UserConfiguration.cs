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

            // Set while a paid plan is active; null on the free plan. After the end date the user counts as free.
            builder.Property(x => x.PlanActivatedAt);
            builder.Property(x => x.PlanExpiresAt);

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

            builder.HasOne(x => x.DeletionRequest)
                .WithOne(x => x.User)
                .HasForeignKey<UserDeletionRequest>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal class UserDeletionRequestConfiguration : IEntityTypeConfiguration<UserDeletionRequest>
    {
        public void Configure(EntityTypeBuilder<UserDeletionRequest> builder)
        {
            builder.ToTable("user_deletion_requests");
            builder.HasKey(x => x.Id);

            // Client-assigned Guids: without this, attaching a new request to an existing user is tracked as
            // Modified and the UPDATE hits 0 rows (DbUpdateConcurrencyException).
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.RequestedAt).HasColumnName("requested_at").IsRequired();
            builder.Property(x => x.ScheduledFor).HasColumnName("scheduled_for").IsRequired();

            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_user_deletion_requests_user_id");

            builder.HasIndex(x => x.ScheduledFor)
                .HasDatabaseName("IX_user_deletion_requests_scheduled_for");
        }
    }
}
