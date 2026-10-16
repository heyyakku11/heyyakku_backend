using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class AuthenticationAuditLogConfiguration : IEntityTypeConfiguration<AuthenticationAuditLog>
    {
        public void Configure(EntityTypeBuilder<AuthenticationAuditLog> builder)
        {
            builder.ToTable("authentication_audit_logs", table =>
            {
                table.HasCheckConstraint(
                    "CK_authentication_audit_logs_actor_type",
                    "\"actor_type\" IN ('USER', 'GUEST', 'ADMIN', 'SYSTEM')");
                table.HasCheckConstraint(
                    "CK_authentication_audit_logs_outcome",
                    "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                table.HasCheckConstraint(
                    "CK_authentication_audit_logs_event_type_not_blank",
                    "char_length(btrim(\"event_type\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");

            builder.Property(x => x.ActorType)
                .HasColumnName("actor_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.ActorId).HasColumnName("actor_id");

            builder.Property(x => x.SubjectHash)
                .HasColumnName("subject_hash")
                .HasMaxLength(64);

            builder.Property(x => x.EventType)
                .HasColumnName("event_type")
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(x => x.Outcome)
                .HasColumnName("outcome")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.FailureReason)
                .HasColumnName("failure_reason")
                .HasMaxLength(128);

            builder.Property(x => x.SessionId).HasColumnName("session_id");

            builder.Property(x => x.IpAddress)
                .HasColumnName("ip_address")
                .HasColumnType("inet");

            builder.Property(x => x.UserAgent)
                .HasColumnName("user_agent")
                .HasMaxLength(256);

            builder.Property(x => x.TraceId)
                .HasColumnName("trace_id")
                .HasMaxLength(64);

            builder.Property(x => x.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            // No foreign keys: the trail must outlive the users, admins and guests it describes.
            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_authentication_audit_logs_created_at");

            builder.HasIndex(x => new { x.ActorType, x.ActorId, x.CreatedAt })
                .HasDatabaseName("IX_authentication_audit_logs_actor");

            builder.HasIndex(x => new { x.EventType, x.CreatedAt })
                .HasDatabaseName("IX_authentication_audit_logs_event_type_created_at");

            builder.HasIndex(x => new { x.Outcome, x.CreatedAt })
                .HasFilter("\"outcome\" <> 'SUCCESS'")
                .HasDatabaseName("IX_authentication_audit_logs_non_success");

            builder.HasIndex(x => new { x.SubjectHash, x.CreatedAt })
                .HasFilter("\"subject_hash\" IS NOT NULL")
                .HasDatabaseName("IX_authentication_audit_logs_subject_hash");
        }
    }
}
