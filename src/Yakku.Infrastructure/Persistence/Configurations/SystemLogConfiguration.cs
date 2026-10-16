using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class SystemLogConfiguration : IEntityTypeConfiguration<SystemLog>
    {
        public void Configure(EntityTypeBuilder<SystemLog> builder)
        {
            builder.ToTable("system_logs", table =>
            {
                table.HasCheckConstraint(
                    "CK_system_logs_category",
                    "\"category\" IN ('EXCEPTION', 'LATENCY', 'FAILURE', 'RATELIMIT', 'BACKGROUNDJOB', 'PERFORMANCE')");
                table.HasCheckConstraint(
                    "CK_system_logs_severity",
                    "\"severity\" IN ('INFORMATION', 'WARNING', 'ERROR', 'CRITICAL')");
                table.HasCheckConstraint(
                    "CK_system_logs_duration_non_negative",
                    "\"duration_ms\" IS NULL OR \"duration_ms\" >= 0");
                table.HasCheckConstraint(
                    "CK_system_logs_status_code_range",
                    "\"status_code\" IS NULL OR (\"status_code\" >= 100 AND \"status_code\" <= 599)");
                table.HasCheckConstraint(
                    "CK_system_logs_message_not_blank",
                    "char_length(btrim(\"message\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");

            builder.Property(x => x.Category)
                .HasColumnName("category")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.Severity)
                .HasColumnName("severity")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.Source)
                .HasColumnName("source")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Message)
                .HasColumnName("message")
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.ExceptionType)
                .HasColumnName("exception_type")
                .HasMaxLength(200);

            builder.Property(x => x.HttpMethod)
                .HasColumnName("http_method")
                .HasMaxLength(10);

            builder.Property(x => x.Path)
                .HasColumnName("path")
                .HasMaxLength(200);

            builder.Property(x => x.StatusCode).HasColumnName("status_code");
            builder.Property(x => x.DurationMs).HasColumnName("duration_ms");

            builder.Property(x => x.TraceId)
                .HasColumnName("trace_id")
                .HasMaxLength(64);

            builder.Property(x => x.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_system_logs_created_at");

            builder.HasIndex(x => new { x.Category, x.CreatedAt })
                .HasDatabaseName("IX_system_logs_category_created_at");

            builder.HasIndex(x => new { x.Severity, x.CreatedAt })
                .HasDatabaseName("IX_system_logs_severity_created_at");
        }
    }
}
