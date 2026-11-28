using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public const string AnonymousNameIndex = "IX_Users_AnonymousName";

        /// <summary>Stored lower-case copy of the name, so the unique index is case-insensitive.</summary>
        public const string AnonymousNameKey = "AnonymousNameKey";

        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.AnonymousName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property<string>(AnonymousNameKey)
                .HasColumnName(AnonymousNameKey)
                .HasComputedColumnSql("lower(\"AnonymousName\")", stored: true);

            builder.HasIndex(AnonymousNameKey)
                .IsUnique()
                .HasDatabaseName(AnonymousNameIndex);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(UserStatus.Active);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired();

            builder.HasIndex(x => x.PhoneNumber)
                .IsUnique()
                .HasDatabaseName("IX_Users_PhoneNumber");

            // The plan row is 1:1 with the user over the user id, created with the account and removed with it.
            builder.HasOne(x => x.UserPlan)
                .WithOne(x => x.User)
                .HasForeignKey<UserPlan>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Permissions)
                .WithOne(x => x.User)
                .HasForeignKey<UserPermission>(x => x.UserId)
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
