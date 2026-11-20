using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class EntitlementAuditLogConfiguration : IEntityTypeConfiguration<EntitlementAuditLog>
    {
        public void Configure(EntityTypeBuilder<EntitlementAuditLog> builder)
        {
            builder.ToTable("entitlement_audit_logs", table =>
            {
                table.HasCheckConstraint(
                    "CK_entitlement_audit_logs_outcome",
                    "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                table.HasCheckConstraint(
                    "CK_entitlement_audit_logs_event_type_not_blank",
                    "char_length(btrim(\"event_type\")) > 0");
                table.HasCheckConstraint(
                    "CK_entitlement_audit_logs_latency_non_negative",
                    "\"latency_ms\" IS NULL OR \"latency_ms\" >= 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
            builder.Property(x => x.Outcome)
                .HasColumnName("outcome")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.PlanId).HasColumnName("plan_id");
            builder.Property(x => x.EntitlementId).HasColumnName("entitlement_id");
            builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id");
            builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(32);
            builder.Property(x => x.ContentId).HasColumnName("content_id");
            builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(64);
            builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
            builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(64);
            builder.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            builder.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            builder.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_entitlement_audit_logs_created_at");
            builder.HasIndex(x => new { x.UserId, x.CreatedAt })
                .HasFilter("\"user_id\" IS NOT NULL")
                .HasDatabaseName("IX_entitlement_audit_logs_user_id_created_at");
            builder.HasIndex(x => new { x.EventType, x.CreatedAt })
                .HasDatabaseName("IX_entitlement_audit_logs_event_type_created_at");
        }
    }
}
