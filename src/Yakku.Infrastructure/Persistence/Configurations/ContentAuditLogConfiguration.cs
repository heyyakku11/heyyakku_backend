using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class ContentAuditLogConfiguration : IEntityTypeConfiguration<ContentAuditLog>
    {
        public void Configure(EntityTypeBuilder<ContentAuditLog> builder)
        {
            builder.ToTable("content_audit_logs", table =>
            {
                table.HasCheckConstraint(
                    "CK_content_audit_logs_actor_type",
                    "\"actor_type\" IN ('USER', 'GUEST', 'ADMIN', 'SYSTEM')");
                table.HasCheckConstraint(
                    "CK_content_audit_logs_outcome",
                    "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                table.HasCheckConstraint(
                    "CK_content_audit_logs_event_type_not_blank",
                    "char_length(btrim(\"event_type\")) > 0");
                table.HasCheckConstraint(
                    "CK_content_audit_logs_content_type_not_blank",
                    "char_length(btrim(\"content_type\")) > 0");
                table.HasCheckConstraint(
                    "CK_content_audit_logs_latency_non_negative",
                    "\"latency_ms\" IS NULL OR \"latency_ms\" >= 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(32).IsRequired();
            builder.Property(x => x.ContentId).HasColumnName("content_id");
            builder.Property(x => x.ActorType)
                .HasColumnName("actor_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.ActorId).HasColumnName("actor_id");
            builder.Property(x => x.VoterRef).HasColumnName("voter_ref").HasMaxLength(128);
            builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
            builder.Property(x => x.Outcome)
                .HasColumnName("outcome")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(32);
            builder.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(32);
            builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
            builder.Property(x => x.TraceId).HasColumnName("trace_id").HasMaxLength(64);
            builder.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            builder.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            builder.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_content_audit_logs_created_at");
            builder.HasIndex(x => new { x.ContentType, x.ContentId, x.CreatedAt })
                .HasDatabaseName("IX_content_audit_logs_content");
            builder.HasIndex(x => new { x.EventType, x.CreatedAt })
                .HasDatabaseName("IX_content_audit_logs_event_type_created_at");
        }
    }
}
