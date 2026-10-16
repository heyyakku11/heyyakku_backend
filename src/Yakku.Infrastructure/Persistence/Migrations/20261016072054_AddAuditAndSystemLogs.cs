using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndSystemLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authentication_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_audit_logs", x => x.id);
                    table.CheckConstraint("CK_authentication_audit_logs_actor_type", "\"actor_type\" IN ('USER', 'GUEST', 'ADMIN', 'SYSTEM')");
                    table.CheckConstraint("CK_authentication_audit_logs_event_type_not_blank", "char_length(btrim(\"event_type\")) > 0");
                    table.CheckConstraint("CK_authentication_audit_logs_outcome", "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                });

            migrationBuilder.CreateTable(
                name: "notification_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    will_retry = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    delivery_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider_accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_audit_logs", x => x.id);
                    table.CheckConstraint("CK_notification_audit_logs_accepted_at_matches_status", "(\"status\" = 'ACCEPTED') = (\"provider_accepted_at\" IS NOT NULL)");
                    table.CheckConstraint("CK_notification_audit_logs_attempt_positive", "\"attempt_number\" > 0");
                    table.CheckConstraint("CK_notification_audit_logs_channel", "\"channel\" IN ('PUSH', 'EMAIL', 'SMS', 'WHATSAPP')");
                    table.CheckConstraint("CK_notification_audit_logs_delivery_requires_acceptance", "\"delivery_status\" IN ('UNKNOWN', 'NOTAPPLICABLE') OR \"status\" = 'ACCEPTED'");
                    table.CheckConstraint("CK_notification_audit_logs_delivery_status", "\"delivery_status\" IN ('UNKNOWN', 'DELIVERED', 'FAILED', 'NOTAPPLICABLE')");
                    table.CheckConstraint("CK_notification_audit_logs_latency_non_negative", "\"latency_ms\" IS NULL OR \"latency_ms\" >= 0");
                    table.CheckConstraint("CK_notification_audit_logs_status", "\"status\" IN ('QUEUED', 'ACCEPTED', 'FAILED', 'SKIPPED')");
                });

            migrationBuilder.CreateTable(
                name: "poll_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voter_ref = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_audit_logs", x => x.id);
                    table.CheckConstraint("CK_poll_audit_logs_actor_type", "\"actor_type\" IN ('USER', 'GUEST', 'ADMIN', 'SYSTEM')");
                    table.CheckConstraint("CK_poll_audit_logs_event_type_not_blank", "char_length(btrim(\"event_type\")) > 0");
                    table.CheckConstraint("CK_poll_audit_logs_outcome", "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                    table.CheckConstraint("CK_poll_audit_logs_voter_ref_vote_events_only", "\"voter_ref\" IS NULL OR \"event_type\" LIKE 'vote.%'");
                    table.CheckConstraint("CK_poll_audit_logs_voter_ref_without_user", "\"voter_ref\" IS NULL OR \"actor_user_id\" IS NULL");
                });

            migrationBuilder.CreateTable(
                name: "system_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    exception_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    path = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_logs", x => x.id);
                    table.CheckConstraint("CK_system_logs_category", "\"category\" IN ('EXCEPTION', 'LATENCY', 'FAILURE', 'RATELIMIT', 'BACKGROUNDJOB', 'PERFORMANCE')");
                    table.CheckConstraint("CK_system_logs_duration_non_negative", "\"duration_ms\" IS NULL OR \"duration_ms\" >= 0");
                    table.CheckConstraint("CK_system_logs_message_not_blank", "char_length(btrim(\"message\")) > 0");
                    table.CheckConstraint("CK_system_logs_severity", "\"severity\" IN ('INFORMATION', 'WARNING', 'ERROR', 'CRITICAL')");
                    table.CheckConstraint("CK_system_logs_status_code_range", "\"status_code\" IS NULL OR (\"status_code\" >= 100 AND \"status_code\" <= 599)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_authentication_audit_logs_actor",
                table: "authentication_audit_logs",
                columns: new[] { "actor_type", "actor_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_authentication_audit_logs_created_at",
                table: "authentication_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_authentication_audit_logs_event_type_created_at",
                table: "authentication_audit_logs",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_authentication_audit_logs_non_success",
                table: "authentication_audit_logs",
                columns: new[] { "outcome", "created_at" },
                filter: "\"outcome\" <> 'SUCCESS'");

            migrationBuilder.CreateIndex(
                name: "IX_authentication_audit_logs_subject_hash",
                table: "authentication_audit_logs",
                columns: new[] { "subject_hash", "created_at" },
                filter: "\"subject_hash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_audit_logs_channel_status_created_at",
                table: "notification_audit_logs",
                columns: new[] { "channel", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_audit_logs_correlation_id",
                table: "notification_audit_logs",
                column: "correlation_id",
                filter: "\"correlation_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_audit_logs_created_at",
                table: "notification_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_notification_audit_logs_provider_message_id",
                table: "notification_audit_logs",
                columns: new[] { "provider", "provider_message_id" },
                filter: "\"provider_message_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_notification_audit_logs_user_id_created_at",
                table: "notification_audit_logs",
                columns: new[] { "user_id", "created_at" },
                filter: "\"user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_poll_audit_logs_actor_user_id_created_at",
                table: "poll_audit_logs",
                columns: new[] { "actor_user_id", "created_at" },
                filter: "\"actor_user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_poll_audit_logs_created_at",
                table: "poll_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_poll_audit_logs_event_type_created_at",
                table: "poll_audit_logs",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_poll_audit_logs_poll_id_created_at",
                table: "poll_audit_logs",
                columns: new[] { "poll_id", "created_at" },
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_category_created_at",
                table: "system_logs",
                columns: new[] { "category", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_created_at",
                table: "system_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_system_logs_severity_created_at",
                table: "system_logs",
                columns: new[] { "severity", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authentication_audit_logs");

            migrationBuilder.DropTable(
                name: "notification_audit_logs");

            migrationBuilder.DropTable(
                name: "poll_audit_logs");

            migrationBuilder.DropTable(
                name: "system_logs");
        }
    }
}
