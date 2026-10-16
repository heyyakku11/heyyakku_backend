using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollAuditLogConfiguration : IEntityTypeConfiguration<PollAuditLog>
    {
        public void Configure(EntityTypeBuilder<PollAuditLog> builder)
        {
            builder.ToTable("poll_audit_logs", table =>
            {
                table.HasCheckConstraint(
                    "CK_poll_audit_logs_actor_type",
                    "\"actor_type\" IN ('USER', 'GUEST', 'ADMIN', 'SYSTEM')");
                table.HasCheckConstraint(
                    "CK_poll_audit_logs_outcome",
                    "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                table.HasCheckConstraint(
                    "CK_poll_audit_logs_event_type_not_blank",
                    "char_length(btrim(\"event_type\")) > 0");
                // Voter privacy: a pseudonymous voter reference only ever appears on vote events and
                // never next to a real user id.
                table.HasCheckConstraint(
                    "CK_poll_audit_logs_voter_ref_vote_events_only",
                    "\"voter_ref\" IS NULL OR \"event_type\" LIKE 'vote.%'");
                table.HasCheckConstraint(
                    "CK_poll_audit_logs_voter_ref_without_user",
                    "\"voter_ref\" IS NULL OR \"actor_user_id\" IS NULL");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.PollId).HasColumnName("poll_id");

            builder.Property(x => x.ActorType)
                .HasColumnName("actor_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");

            builder.Property(x => x.VoterRef)
                .HasColumnName("voter_ref")
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

            builder.Property(x => x.FromStatus)
                .HasColumnName("from_status")
                .HasMaxLength(20);

            builder.Property(x => x.ToStatus)
                .HasColumnName("to_status")
                .HasMaxLength(20);

            builder.Property(x => x.ErrorCode)
                .HasColumnName("error_code")
                .HasMaxLength(64);

            builder.Property(x => x.TraceId)
                .HasColumnName("trace_id")
                .HasMaxLength(64);

            builder.Property(x => x.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            // No foreign keys: deleting a poll or user must never delete or block its audit history.
            builder.HasIndex(x => new { x.PollId, x.CreatedAt })
                .HasFilter("\"poll_id\" IS NOT NULL")
                .HasDatabaseName("IX_poll_audit_logs_poll_id_created_at");

            builder.HasIndex(x => new { x.ActorUserId, x.CreatedAt })
                .HasFilter("\"actor_user_id\" IS NOT NULL")
                .HasDatabaseName("IX_poll_audit_logs_actor_user_id_created_at");

            builder.HasIndex(x => new { x.EventType, x.CreatedAt })
                .HasDatabaseName("IX_poll_audit_logs_event_type_created_at");

            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_poll_audit_logs_created_at");
        }
    }
}
