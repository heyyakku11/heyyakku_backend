using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class VoteNotificationCardConfiguration : IEntityTypeConfiguration<VoteNotificationCard>
    {
        public void Configure(EntityTypeBuilder<VoteNotificationCard> builder)
        {
            builder.ToTable("vote_notification_cards", table =>
            {
                table.HasCheckConstraint("CK_vote_notification_cards_count_positive", "\"vote_count\" > 0");
            });
            builder.HasKey(x => new { x.ContentType, x.ContentId });

            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.ContentId).HasColumnName("content_id");
            builder.Property(x => x.OwnerId).HasColumnName("owner_id").IsRequired();
            builder.Property(x => x.VoteCount).HasColumnName("vote_count").IsRequired();
        }
    }

    internal class VoteNotificationEventConfiguration : IEntityTypeConfiguration<VoteNotificationEvent>
    {
        public void Configure(EntityTypeBuilder<VoteNotificationEvent> builder)
        {
            builder.ToTable("vote_notification_events", table =>
            {
                table.HasCheckConstraint(
                    "CK_vote_notification_events_kind",
                    "\"kind\" IN ('IMMEDIATE', 'DIGEST')");
                table.HasCheckConstraint(
                    "CK_vote_notification_events_status",
                    "\"status\" IN ('PENDING', 'SENDING', 'SUBMITTED', 'SKIPPED', 'FAILED')");
                table.HasCheckConstraint(
                    "CK_vote_notification_events_digest_has_window",
                    "(\"kind\" = 'DIGEST') = (\"window_id\" IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.OwnerId).HasColumnName("owner_id").IsRequired();
            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.ContentId).HasColumnName("content_id").IsRequired();
            builder.Property(x => x.VoteId).HasColumnName("vote_id").IsRequired();
            builder.Property(x => x.Sequence).HasColumnName("sequence").IsRequired();
            builder.Property(x => x.Kind)
                .HasColumnName("kind")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.NotificationId).HasColumnName("notification_id").IsRequired();
            builder.Property(x => x.WindowId).HasColumnName("window_id");
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();
            builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            builder.Property(x => x.LeaseUntil).HasColumnName("lease_until");
            builder.Property(x => x.LeaseToken).HasColumnName("lease_token");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");

            // No foreign keys, like the audit tables: retention of notifications and account deletion must not be
            // blocked by delivery rows, and stale rows end up skipped and purged.
            builder.HasIndex(x => new { x.ContentType, x.VoteId })
                .IsUnique()
                .HasDatabaseName("IX_vote_notification_events_content_type_vote_id");

            builder.HasIndex(x => x.NextAttemptAt)
                .HasFilter("\"kind\" = 'IMMEDIATE' AND \"status\" IN ('PENDING', 'SENDING')")
                .HasDatabaseName("IX_vote_notification_events_due");

            builder.HasIndex(x => new { x.WindowId, x.Status })
                .HasFilter("\"window_id\" IS NOT NULL")
                .HasDatabaseName("IX_vote_notification_events_window_id_status");

            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_vote_notification_events_created_at");
        }
    }

    internal class VoteDigestWindowConfiguration : IEntityTypeConfiguration<VoteDigestWindow>
    {
        public void Configure(EntityTypeBuilder<VoteDigestWindow> builder)
        {
            builder.ToTable("vote_digest_windows", table =>
            {
                table.HasCheckConstraint(
                    "CK_vote_digest_windows_status",
                    "\"status\" IN ('OPEN', 'DUE', 'PROCESSING', 'SENT', 'SKIPPED', 'FAILED')");
                table.HasCheckConstraint(
                    "CK_vote_digest_windows_ends_after_start",
                    "\"ends_at\" > \"starts_at\"");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.OwnerId).HasColumnName("owner_id").IsRequired();
            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.ContentId).HasColumnName("content_id").IsRequired();
            builder.Property(x => x.StartsAt).HasColumnName("starts_at").IsRequired();
            builder.Property(x => x.EndsAt).HasColumnName("ends_at").IsRequired();
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();
            builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            builder.Property(x => x.LeaseUntil).HasColumnName("lease_until");
            builder.Property(x => x.LeaseToken).HasColumnName("lease_token");
            builder.Property(x => x.EventCount).HasColumnName("event_count").IsRequired();
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

            // At most one window per card accepts events at a time.
            builder.HasIndex(x => new { x.ContentType, x.ContentId })
                .IsUnique()
                .HasFilter("\"status\" = 'OPEN'")
                .HasDatabaseName("IX_vote_digest_windows_open_per_card");

            builder.HasIndex(x => x.EndsAt)
                .HasFilter("\"status\" IN ('OPEN', 'DUE', 'PROCESSING')")
                .HasDatabaseName("IX_vote_digest_windows_due");
        }
    }
}
