using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class NotificationAuditLogConfiguration : IEntityTypeConfiguration<NotificationAuditLog>
    {
        public void Configure(EntityTypeBuilder<NotificationAuditLog> builder)
        {
            builder.ToTable("notification_audit_logs", table =>
            {
                table.HasCheckConstraint(
                    "CK_notification_audit_logs_channel",
                    "\"channel\" IN ('PUSH', 'EMAIL', 'SMS', 'WHATSAPP')");
                table.HasCheckConstraint(
                    "CK_notification_audit_logs_status",
                    "\"status\" IN ('QUEUED', 'ACCEPTED', 'FAILED', 'SKIPPED')");
                table.HasCheckConstraint(
                    "CK_notification_audit_logs_delivery_status",
                    "\"delivery_status\" IN ('UNKNOWN', 'DELIVERED', 'FAILED', 'NOTAPPLICABLE')");
                table.HasCheckConstraint(
                    "CK_notification_audit_logs_attempt_positive",
                    "\"attempt_number\" > 0");
                table.HasCheckConstraint(
                    "CK_notification_audit_logs_latency_non_negative",
                    "\"latency_ms\" IS NULL OR \"latency_ms\" >= 0");
                // Provider acceptance and delivery are different facts: a delivery outcome can only exist
                // for an attempt the provider accepted.
                table.HasCheckConstraint(
                    "CK_notification_audit_logs_delivery_requires_acceptance",
                    "\"delivery_status\" IN ('UNKNOWN', 'NOTAPPLICABLE') OR \"status\" = 'ACCEPTED'");
                table.HasCheckConstraint(
                    "CK_notification_audit_logs_accepted_at_matches_status",
                    "(\"status\" = 'ACCEPTED') = (\"provider_accepted_at\" IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");

            builder.Property(x => x.Channel)
                .HasColumnName("channel")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.Provider)
                .HasColumnName("provider")
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.EventType)
                .HasColumnName("event_type")
                .HasMaxLength(64);

            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.DeviceId).HasColumnName("device_id");

            builder.Property(x => x.Recipient)
                .HasColumnName("recipient")
                .HasMaxLength(128);

            builder.Property(x => x.CorrelationId).HasColumnName("correlation_id");

            builder.Property(x => x.AttemptNumber)
                .HasColumnName("attempt_number")
                .IsRequired();

            builder.Property(x => x.WillRetry)
                .HasColumnName("will_retry")
                .IsRequired();

            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.DeliveryStatus)
                .HasColumnName("delivery_status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.ProviderMessageId)
                .HasColumnName("provider_message_id")
                .HasMaxLength(256);

            builder.Property(x => x.ErrorCode)
                .HasColumnName("error_code")
                .HasMaxLength(64);

            builder.Property(x => x.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(500);

            builder.Property(x => x.LatencyMs).HasColumnName("latency_ms");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(x => x.ProviderAcceptedAt).HasColumnName("provider_accepted_at");
            builder.Property(x => x.DeliveryUpdatedAt).HasColumnName("delivery_updated_at");

            // No foreign keys: the delivery history must outlive users and devices.
            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_notification_audit_logs_created_at");

            builder.HasIndex(x => new { x.Provider, x.ProviderMessageId })
                .HasFilter("\"provider_message_id\" IS NOT NULL")
                .HasDatabaseName("IX_notification_audit_logs_provider_message_id");

            builder.HasIndex(x => x.CorrelationId)
                .HasFilter("\"correlation_id\" IS NOT NULL")
                .HasDatabaseName("IX_notification_audit_logs_correlation_id");

            builder.HasIndex(x => new { x.UserId, x.CreatedAt })
                .HasFilter("\"user_id\" IS NOT NULL")
                .HasDatabaseName("IX_notification_audit_logs_user_id_created_at");

            builder.HasIndex(x => new { x.Channel, x.Status, x.CreatedAt })
                .HasDatabaseName("IX_notification_audit_logs_channel_status_created_at");
        }
    }
}
