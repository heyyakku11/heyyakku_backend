using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserActivityDayConfiguration : IEntityTypeConfiguration<UserActivityDay>
    {
        public void Configure(EntityTypeBuilder<UserActivityDay> builder)
        {
            builder.ToTable("user_activity_days");

            // At most one streak day per user per UTC calendar day, whatever the number of actions that day.
            builder.HasKey(x => new { x.UserId, x.ActivityDate })
                .HasName("PK_user_activity_days");

            builder.Property(x => x.UserId)
                .HasColumnName("user_id");

            builder.Property(x => x.ActivityDate)
                .HasColumnName("activity_date")
                .HasColumnType("date");

            builder.Property(x => x.FirstActivityAt)
                .HasColumnName("first_activity_at")
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal class UserStreakConfiguration : IEntityTypeConfiguration<UserStreak>
    {
        public void Configure(EntityTypeBuilder<UserStreak> builder)
        {
            builder.ToTable("user_streaks", table =>
            {
                table.HasCheckConstraint(
                    "CK_user_streaks_current_positive",
                    "\"current_streak\" >= 1");
                table.HasCheckConstraint(
                    "CK_user_streaks_longest_at_least_current",
                    "\"longest_streak\" >= \"current_streak\"");
            });
            builder.HasKey(x => x.UserId)
                .HasName("PK_user_streaks");

            builder.Property(x => x.UserId)
                .HasColumnName("user_id");

            builder.Property(x => x.CurrentStreak)
                .HasColumnName("current_streak")
                .IsRequired();

            builder.Property(x => x.LongestStreak)
                .HasColumnName("longest_streak")
                .IsRequired();

            builder.Property(x => x.LastActivityDate)
                .HasColumnName("last_activity_date")
                .HasColumnType("date")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
