using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
    {
        public void Configure(EntityTypeBuilder<PaymentOrder> builder)
        {
            builder.ToTable("payment_orders", table =>
            {
                table.HasCheckConstraint(
                    "CK_payment_orders_purchase_type",
                    "\"purchase_type\" IN ('USERPLAN', 'CARDUNLOCK', 'CONVERSATIONTOPUP')");
                table.HasCheckConstraint(
                    "CK_payment_orders_status",
                    "\"status\" IN ('CREATED', 'PENDING', 'PAID', 'FAILED', 'CANCELLED', 'EXPIRED')");
                table.HasCheckConstraint(
                    "CK_payment_orders_amount_not_negative",
                    "\"amount_in_paise\" >= 0");
                table.HasCheckConstraint(
                    "CK_payment_orders_user_plan_shape",
                    "(\"purchase_type\" <> 'USERPLAN') OR (\"plan_id\" IS NOT NULL AND \"content_type\" IS NULL AND \"content_id\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_payment_orders_card_unlock_shape",
                    "(\"purchase_type\" <> 'CARDUNLOCK') OR (\"plan_id\" IS NOT NULL AND \"content_type\" IN ('POLL', 'CHECK') AND \"content_id\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_payment_orders_conversation_top_up_shape",
                    "(\"purchase_type\" <> 'CONVERSATIONTOPUP') OR (\"plan_id\" IS NOT NULL AND \"content_type\" IS NULL AND \"content_id\" IS NULL AND \"conversation_id\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_payment_orders_conversation_id_only_for_top_up",
                    "\"conversation_id\" IS NULL OR \"purchase_type\" = 'CONVERSATIONTOPUP'");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.PlanId).HasColumnName("plan_id");
            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.ContentId).HasColumnName("content_id");
            builder.Property(x => x.ConversationId).HasColumnName("conversation_id");
            builder.Property(x => x.PurchaseType)
                .HasColumnName("purchase_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.AmountInPaise).HasColumnName("amount_in_paise").IsRequired();
            builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            builder.Property(x => x.RazorpayOrderId).HasColumnName("razorpay_order_id").HasMaxLength(64);
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.PlanSnapshot)
                .HasColumnName("plan_snapshot")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
            builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.Property(x => x.PaidAt).HasColumnName("paid_at");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();

            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => new { x.UserId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("IX_payment_orders_user_idempotency");
            builder.HasIndex(x => x.RazorpayOrderId)
                .IsUnique()
                .HasDatabaseName("IX_payment_orders_razorpay_order_id")
                .HasFilter("\"razorpay_order_id\" IS NOT NULL");
            builder.HasIndex(x => new { x.UserId, x.CreatedAt })
                .HasDatabaseName("IX_payment_orders_user_created_at");
            builder.HasIndex(x => x.Status)
                .HasDatabaseName("IX_payment_orders_status");
        }
    }

    internal class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
    {
        public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
        {
            builder.ToTable("payment_attempts", table =>
            {
                table.HasCheckConstraint(
                    "CK_payment_attempts_status",
                    "\"status\" IN ('CREATED', 'AUTHORIZED', 'CAPTURED', 'FAILED')");
                table.HasCheckConstraint(
                    "CK_payment_attempts_amount_not_negative",
                    "\"amount_in_paise\" >= 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();
            builder.Property(x => x.RazorpayPaymentId).HasColumnName("razorpay_payment_id").HasMaxLength(64);
            builder.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasMaxLength(32);
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.AmountInPaise).HasColumnName("amount_in_paise").IsRequired();
            builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            builder.Property(x => x.RazorpayStatus).HasColumnName("razorpay_status").HasMaxLength(32);
            builder.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(64);
            builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.Property(x => x.AuthorizedAt).HasColumnName("authorized_at");
            builder.Property(x => x.CapturedAt).HasColumnName("captured_at");
            builder.Property(x => x.FailedAt).HasColumnName("failed_at");

            builder.HasOne(x => x.PaymentOrder)
                .WithMany(x => x.Attempts)
                .HasForeignKey(x => x.PaymentOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.RazorpayPaymentId)
                .IsUnique()
                .HasDatabaseName("IX_payment_attempts_razorpay_payment_id")
                .HasFilter("\"razorpay_payment_id\" IS NOT NULL");
            builder.HasIndex(x => x.PaymentOrderId)
                .HasDatabaseName("IX_payment_attempts_payment_order_id");
        }
    }

    internal class UserPlanEntitlementConfiguration : IEntityTypeConfiguration<UserPlanEntitlement>
    {
        public void Configure(EntityTypeBuilder<UserPlanEntitlement> builder)
        {
            builder.ToTable("user_plan_entitlements", table =>
            {
                table.HasCheckConstraint(
                    "CK_user_plan_entitlements_status",
                    "\"status\" IN ('ACTIVE', 'EXPIRED', 'SUPERSEDED', 'REVOKED')");
                table.HasCheckConstraint(
                    "CK_user_plan_entitlements_window",
                    "\"expires_at\" > \"starts_at\"");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.PlanId).HasColumnName("plan_id").IsRequired();
            builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id");
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.StartsAt).HasColumnName("starts_at").IsRequired();
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            builder.Property(x => x.SupersededAt).HasColumnName("superseded_at");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(x => x.PaymentOrderId).OnDelete(DeleteBehavior.SetNull);

            // At most one active paid entitlement per user (Love supersedes Lite in the same transaction).
            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_user_plan_entitlements_one_active")
                .HasFilter("\"status\" = 'ACTIVE'");
            builder.HasIndex(x => new { x.UserId, x.Status })
                .HasDatabaseName("IX_user_plan_entitlements_user_status");
            builder.HasIndex(x => x.ExpiresAt)
                .HasDatabaseName("IX_user_plan_entitlements_expires_at")
                .HasFilter("\"status\" = 'ACTIVE'");
            builder.HasIndex(x => x.PaymentOrderId)
                .IsUnique()
                .HasDatabaseName("IX_user_plan_entitlements_payment_order_id")
                .HasFilter("\"payment_order_id\" IS NOT NULL");
        }
    }

    internal class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
    {
        public void Configure(EntityTypeBuilder<WebhookEvent> builder)
        {
            builder.ToTable("webhook_events", table =>
            {
                table.HasCheckConstraint(
                    "CK_webhook_events_processing_status",
                    "\"processing_status\" IN ('RECEIVED', 'PROCESSING', 'PROCESSED', 'FAILED')");
                table.HasCheckConstraint(
                    "CK_webhook_events_retry_count",
                    "\"retry_count\" >= 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RazorpayEventId).HasColumnName("razorpay_event_id").HasMaxLength(64).IsRequired();
            builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
            builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.ProcessingStatus)
                .HasColumnName("processing_status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.ReceivedAt).HasColumnName("received_at").IsRequired();
            builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
            builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();

            builder.HasIndex(x => x.RazorpayEventId)
                .IsUnique()
                .HasDatabaseName("IX_webhook_events_razorpay_event_id");
            builder.HasIndex(x => new { x.ProcessingStatus, x.ReceivedAt })
                .HasDatabaseName("IX_webhook_events_status_received_at");
        }
    }

    internal class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
    {
        public void Configure(EntityTypeBuilder<PaymentRefund> builder)
        {
            builder.ToTable("payment_refunds", table =>
            {
                table.HasCheckConstraint(
                    "CK_payment_refunds_status",
                    "\"status\" IN ('REQUESTED', 'PROCESSED', 'FAILED')");
                table.HasCheckConstraint(
                    "CK_payment_refunds_amount_positive",
                    "\"amount_in_paise\" > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.PaymentAttemptId).HasColumnName("payment_attempt_id").IsRequired();
            builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id").IsRequired();
            builder.Property(x => x.RazorpayRefundId).HasColumnName("razorpay_refund_id").HasMaxLength(64);
            builder.Property(x => x.AmountInPaise).HasColumnName("amount_in_paise").IsRequired();
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            builder.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(64);
            builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
            builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");

            builder.HasOne(x => x.PaymentAttempt)
                .WithMany()
                .HasForeignKey(x => x.PaymentAttemptId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PaymentOrder>()
                .WithMany()
                .HasForeignKey(x => x.PaymentOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.PaymentAttemptId)
                .HasDatabaseName("IX_payment_refunds_payment_attempt_id");
            builder.HasIndex(x => x.RazorpayRefundId)
                .IsUnique()
                .HasDatabaseName("IX_payment_refunds_razorpay_refund_id")
                .HasFilter("\"razorpay_refund_id\" IS NOT NULL");
            builder.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_payment_refunds_created_at");
        }
    }

    internal class PaymentLogConfiguration : IEntityTypeConfiguration<PaymentLog>
    {
        public void Configure(EntityTypeBuilder<PaymentLog> builder)
        {
            builder.ToTable("payment_logs", table =>
            {
                table.HasCheckConstraint(
                    "CK_payment_logs_status",
                    "\"status\" IN ('INFORMATION', 'SUCCESS', 'WARNING', 'FAILURE')");
                table.HasCheckConstraint(
                    "CK_payment_logs_message_not_blank",
                    "char_length(btrim(\"message\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id");
            builder.Property(x => x.PaymentAttemptId).HasColumnName("payment_attempt_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
            builder.Property(x => x.EventSource).HasColumnName("event_source").HasMaxLength(32).IsRequired();
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(500).IsRequired();
            builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
            builder.Property(x => x.RazorpayOrderId).HasColumnName("razorpay_order_id").HasMaxLength(64);
            builder.Property(x => x.RazorpayPaymentId).HasColumnName("razorpay_payment_id").HasMaxLength(64);
            builder.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            builder.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            // Soft references only: payment audit must survive order cleanup and never block financial deletes.
            builder.HasIndex(x => x.PaymentOrderId).HasDatabaseName("IX_payment_logs_payment_order_id");
            builder.HasIndex(x => x.UserId).HasDatabaseName("IX_payment_logs_user_id");
            builder.HasIndex(x => x.CorrelationId).HasDatabaseName("IX_payment_logs_correlation_id");
            builder.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_payment_logs_created_at");
            builder.HasIndex(x => new { x.EventType, x.CreatedAt })
                .HasDatabaseName("IX_payment_logs_event_type_created_at");
        }
    }
}
