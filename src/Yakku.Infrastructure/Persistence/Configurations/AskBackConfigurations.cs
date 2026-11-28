using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class AskBackConversationConfiguration : IEntityTypeConfiguration<AskBackConversation>
    {
        public void Configure(EntityTypeBuilder<AskBackConversation> builder)
        {
            builder.ToTable("ask_back_conversations", table =>
            {
                table.HasCheckConstraint(
                    "CK_ask_back_conversations_status",
                    "\"status\" IN ('ACTIVE', 'CLOSED', 'BLOCKED')");
                table.HasCheckConstraint(
                    "CK_ask_back_conversations_distinct_users",
                    "\"creator_user_id\" IS NULL OR \"voter_user_id\" IS NULL OR \"creator_user_id\" <> \"voter_user_id\"");
                table.HasCheckConstraint(
                    "CK_ask_back_conversations_blocked_by",
                    "(\"status\" = 'BLOCKED') = (\"blocked_by_user_id\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_ask_back_conversations_message_counts",
                    "\"asker_message_count\" >= 0 AND \"voter_message_count\" >= 0 AND \"asker_quota_offset\" >= 0");
                table.HasCheckConstraint(
                    "CK_ask_back_conversations_top_up_counts",
                    "\"top_up_granted\" >= 0 AND \"top_up_used\" >= 0 AND \"top_up_used\" <= \"top_up_granted\"");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.ContentId).HasColumnName("content_id").IsRequired();
            builder.Property(x => x.VoteId).HasColumnName("vote_id").IsRequired();
            builder.Property(x => x.CreatorUserId).HasColumnName("creator_user_id");
            builder.Property(x => x.VoterUserId).HasColumnName("voter_user_id");
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.BlockedByUserId).HasColumnName("blocked_by_user_id");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.LastMessageAt).HasColumnName("last_message_at");
            builder.Property(x => x.LastActivityAt).HasColumnName("last_activity_at").IsRequired();
            builder.Property(x => x.ClosedAt).HasColumnName("closed_at");

            // Concurrency tokens: of two sends by the same person at once, only one update matches the old count.
            builder.Property(x => x.AskerMessageCount)
                .HasColumnName("asker_message_count")
                .IsRequired()
                .HasDefaultValue(0)
                .IsConcurrencyToken();
            builder.Property(x => x.VoterMessageCount)
                .HasColumnName("voter_message_count")
                .IsRequired()
                .HasDefaultValue(0)
                .IsConcurrencyToken();
            builder.Property(x => x.AskerQuotaOffset)
                .HasColumnName("asker_quota_offset")
                .IsRequired()
                .HasDefaultValue(0);
            builder.Property(x => x.AppliedMessageLimit)
                .HasColumnName("applied_message_limit");
            builder.Property(x => x.TopUpGranted)
                .HasColumnName("top_up_granted")
                .IsRequired()
                .HasDefaultValue(0);
            builder.Property(x => x.TopUpUsed)
                .HasColumnName("top_up_used")
                .IsRequired()
                .HasDefaultValue(0);

            // Accounts that are permanently deleted leave the conversation behind for the other person.
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.VoterUserId).OnDelete(DeleteBehavior.SetNull);

            // One conversation per vote: the final guard against two concurrent creations.
            builder.HasIndex(x => x.VoteId)
                .IsUnique()
                .HasDatabaseName("IX_ask_back_conversations_vote_id");

            builder.HasIndex(x => new { x.CreatorUserId, x.LastActivityAt, x.Id })
                .HasDatabaseName("IX_ask_back_conversations_creator_activity");
            builder.HasIndex(x => new { x.VoterUserId, x.LastActivityAt, x.Id })
                .HasDatabaseName("IX_ask_back_conversations_voter_activity");
        }
    }

    internal class AskBackParticipantConfiguration : IEntityTypeConfiguration<AskBackParticipant>
    {
        public void Configure(EntityTypeBuilder<AskBackParticipant> builder)
        {
            builder.ToTable("ask_back_participants");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.LastReadMessageId).HasColumnName("last_read_message_id");
            builder.Property(x => x.LastReadMessageCreatedAt).HasColumnName("last_read_message_created_at");
            builder.Property(x => x.LastReadAt).HasColumnName("last_read_at");
            builder.Property(x => x.JoinedAt).HasColumnName("joined_at").IsRequired();

            builder.HasOne(x => x.Conversation)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(x => new { x.ConversationId, x.UserId })
                .IsUnique()
                .HasDatabaseName("IX_ask_back_participants_conversation_user");
            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_ask_back_participants_user_id");
        }
    }

    internal class AskBackMessageConfiguration : IEntityTypeConfiguration<AskBackMessage>
    {
        public void Configure(EntityTypeBuilder<AskBackMessage> builder)
        {
            builder.ToTable("ask_back_messages", table =>
            {
                table.HasCheckConstraint(
                    "CK_ask_back_messages_content_length",
                    "char_length(\"content\") BETWEEN 1 AND 2000");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
            builder.Property(x => x.SenderUserId).HasColumnName("sender_user_id");
            builder.Property(x => x.Content).HasColumnName("content").IsRequired().HasMaxLength(2000);
            builder.Property(x => x.ClientMessageId).HasColumnName("client_message_id").IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.EditedAt).HasColumnName("edited_at");
            builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

            builder.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.SetNull);

            // A retried send finds its message instead of creating a second one.
            builder.HasIndex(x => new { x.ConversationId, x.SenderUserId, x.ClientMessageId })
                .IsUnique()
                .HasDatabaseName("IX_ask_back_messages_idempotency");

            // History pages walk (created_at, id) within a conversation.
            builder.HasIndex(x => new { x.ConversationId, x.CreatedAt, x.Id })
                .HasDatabaseName("IX_ask_back_messages_history");
        }
    }

    internal class AskBackOutboxEventConfiguration : IEntityTypeConfiguration<AskBackOutboxEvent>
    {
        public void Configure(EntityTypeBuilder<AskBackOutboxEvent> builder)
        {
            builder.ToTable("ask_back_outbox", table =>
            {
                table.HasCheckConstraint(
                    "CK_ask_back_outbox_status",
                    "\"status\" IN ('PENDING', 'PROCESSING', 'SENT', 'SKIPPED', 'FAILED')");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
            builder.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
            builder.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();
            builder.Property(x => x.NotificationId).HasColumnName("notification_id");
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.Attempts).HasColumnName("attempts").IsRequired();
            builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
            builder.Property(x => x.LeaseUntil).HasColumnName("lease_until");
            builder.Property(x => x.LeaseToken).HasColumnName("lease_token");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

            builder.HasIndex(x => x.MessageId)
                .IsUnique()
                .HasDatabaseName("IX_ask_back_outbox_message_id");
            builder.HasIndex(x => x.NextAttemptAt)
                .HasFilter("\"status\" IN ('PENDING', 'PROCESSING')")
                .HasDatabaseName("IX_ask_back_outbox_due");
            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_ask_back_outbox_created_at");
            builder.HasIndex(x => x.NotificationId)
                .HasFilter("\"notification_id\" IS NOT NULL")
                .HasDatabaseName("IX_ask_back_outbox_notification_id");
        }
    }

    internal class AskBackMessageDeliveryTrackingConfiguration : IEntityTypeConfiguration<AskBackMessageDeliveryTracking>
    {
        public void Configure(EntityTypeBuilder<AskBackMessageDeliveryTracking> builder)
        {
            builder.ToTable("ask_back_message_delivery_tracking", table =>
            {
                table.HasCheckConstraint(
                    "CK_ask_back_message_delivery_tracking_status",
                    "\"status\" IN ('PENDING', 'SENT', 'DELIVERED', 'FAILED', 'TIMEDOUT', 'NOACKCONNECTION')");
                table.HasCheckConstraint(
                    "CK_ask_back_message_delivery_tracking_delivered_at",
                    "(\"status\" = 'DELIVERED') = (\"delivered_at\" IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
            builder.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.SentAt).HasColumnName("sent_at");
            builder.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

            builder.HasOne(x => x.Message)
                .WithMany()
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
            // A deleted recipient keeps the row (as null) so delivery metrics do not change after the fact.
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.SetNull);

            // One row per message and recipient: a retried send or a duplicate write cannot add a second.
            builder.HasIndex(x => new { x.MessageId, x.RecipientUserId })
                .IsUnique()
                .HasDatabaseName("IX_ask_back_delivery_message_recipient");

            // The timeout sweeper only looks at rows still waiting.
            builder.HasIndex(x => new { x.Status, x.SentAt })
                .HasFilter("\"status\" IN ('PENDING', 'SENT')")
                .HasDatabaseName("IX_ask_back_delivery_waiting");

            // Reporting periods are chosen by attempt time.
            builder.HasIndex(x => x.SentAt)
                .HasDatabaseName("IX_ask_back_delivery_sent_at");
        }
    }

    internal class AskBackReportConfiguration : IEntityTypeConfiguration<AskBackReport>
    {
        public void Configure(EntityTypeBuilder<AskBackReport> builder)
        {
            builder.ToTable("ask_back_reports");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
            builder.Property(x => x.ReporterUserId).HasColumnName("reporter_user_id");
            builder.Property(x => x.ReportedUserId).HasColumnName("reported_user_id");
            builder.Property(x => x.Reason).HasColumnName("reason").IsRequired().HasMaxLength(500);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            // No foreign keys: a report is a moderation record and outlives the conversation and both accounts.
            builder.HasIndex(x => x.ConversationId)
                .HasDatabaseName("IX_ask_back_reports_conversation_id");
        }
    }

    internal class AskBackConversationTopUpConfiguration : IEntityTypeConfiguration<AskBackConversationTopUp>
    {
        public void Configure(EntityTypeBuilder<AskBackConversationTopUp> builder)
        {
            builder.ToTable("ask_back_conversation_top_ups", table =>
            {
                table.HasCheckConstraint(
                    "CK_ask_back_conversation_top_ups_credits_positive",
                    "\"credits\" > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ConversationId).HasColumnName("conversation_id").IsRequired();
            builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();
            builder.Property(x => x.Credits).HasColumnName("credits").IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            // A paid order grants once: the unique index on the order is the guard against a retried fulfillment.
            builder.HasOne<AskBackConversation>()
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<PaymentOrder>()
                .WithMany()
                .HasForeignKey(x => x.PaymentOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.PaymentOrderId)
                .IsUnique()
                .HasDatabaseName("IX_ask_back_conversation_top_ups_payment_order_id");
            builder.HasIndex(x => x.ConversationId)
                .HasDatabaseName("IX_ask_back_conversation_top_ups_conversation_id");
        }
    }
}
