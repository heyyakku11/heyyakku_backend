using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit_logs", x => x.id);
                    table.CheckConstraint("CK_admin_audit_logs_event_type_not_blank", "char_length(btrim(\"event_type\")) > 0");
                    table.CheckConstraint("CK_admin_audit_logs_latency_non_negative", "\"latency_ms\" IS NULL OR \"latency_ms\" >= 0");
                    table.CheckConstraint("CK_admin_audit_logs_outcome", "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                });

            migrationBuilder.CreateTable(
                name: "admins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ask_back_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_outbox", x => x.id);
                    table.CheckConstraint("CK_ask_back_outbox_status", "\"status\" IN ('PENDING', 'PROCESSING', 'SENT', 'SKIPPED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "ask_back_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reported_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_reports", x => x.id);
                });

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
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
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
                name: "card_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_categories", x => x.id);
                    table.CheckConstraint("CK_card_categories_code_not_blank", "char_length(btrim(\"code\")) > 0");
                    table.CheckConstraint("CK_card_categories_name_not_blank", "char_length(btrim(\"name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "card_themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    background_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    label_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_themes", x => x.id);
                    table.CheckConstraint("CK_card_themes_background_color", "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_card_themes_label_color", "\"label_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_card_themes_name_not_blank", "char_length(btrim(\"name\")) > 0");
                    table.CheckConstraint("CK_card_themes_tag_not_blank", "\"tag\" IS NULL OR char_length(btrim(\"tag\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "content_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voter_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_audit_logs", x => x.id);
                    table.CheckConstraint("CK_content_audit_logs_actor_type", "\"actor_type\" IN ('USER', 'GUEST', 'ADMIN', 'SYSTEM')");
                    table.CheckConstraint("CK_content_audit_logs_content_type_not_blank", "char_length(btrim(\"content_type\")) > 0");
                    table.CheckConstraint("CK_content_audit_logs_event_type_not_blank", "char_length(btrim(\"event_type\")) > 0");
                    table.CheckConstraint("CK_content_audit_logs_latency_non_negative", "\"latency_ms\" IS NULL OR \"latency_ms\" >= 0");
                    table.CheckConstraint("CK_content_audit_logs_outcome", "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                });

            migrationBuilder.CreateTable(
                name: "entitlement_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entitlement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    content_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entitlement_audit_logs", x => x.id);
                    table.CheckConstraint("CK_entitlement_audit_logs_event_type_not_blank", "char_length(btrim(\"event_type\")) > 0");
                    table.CheckConstraint("CK_entitlement_audit_logs_latency_non_negative", "\"latency_ms\" IS NULL OR \"latency_ms\" >= 0");
                    table.CheckConstraint("CK_entitlement_audit_logs_outcome", "\"outcome\" IN ('SUCCESS', 'FAILURE', 'DENIED', 'SUSPICIOUS')");
                });

            migrationBuilder.CreateTable(
                name: "guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionTokenHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<IPAddress>(type: "inet", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BrowserName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BrowserVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OsName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeviceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Language = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    CountryCode = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    CookiesEnabled = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guests", x => x.Id);
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
                name: "payment_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_attempt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    event_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    razorpay_order_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    razorpay_payment_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_logs", x => x.id);
                    table.CheckConstraint("CK_payment_logs_message_not_blank", "char_length(btrim(\"message\")) > 0");
                    table.CheckConstraint("CK_payment_logs_status", "\"status\" IN ('INFORMATION', 'SUCCESS', 'WARNING', 'FAILURE')");
                });

            migrationBuilder.CreateTable(
                name: "personality_traits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_traits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    price_minor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    duration_days = table.Column<int>(type: "integer", nullable: true),
                    unlock_tickets = table.Column<int>(type: "integer", nullable: true),
                    ask_back_message_limit = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.id);
                    table.CheckConstraint("CK_plans_ask_back_message_limit", "\"ask_back_message_limit\" IS NULL OR \"ask_back_message_limit\" >= 1");
                    table.CheckConstraint("CK_plans_duration_days", "\"duration_days\" IS NULL OR \"duration_days\" >= 1");
                    table.CheckConstraint("CK_plans_price_not_negative", "\"price_minor\" >= 0");
                    table.CheckConstraint("CK_plans_unlock_tickets", "\"unlock_tickets\" IS NULL OR \"unlock_tickets\" >= 0");
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
                    latency_ms = table.Column<int>(type: "integer", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "vote_digest_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    event_count = table.Column<int>(type: "integer", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_vote_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    max_flush_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    generation = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vote_digest_windows", x => x.id);
                    table.CheckConstraint("CK_vote_digest_windows_ends_after_start", "\"ends_at\" > \"starts_at\"");
                    table.CheckConstraint("CK_vote_digest_windows_status", "\"status\" IN ('OPEN', 'DUE', 'PROCESSING', 'SENT', 'SKIPPED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "vote_notification_cards",
                columns: table => new
                {
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_count = table.Column<int>(type: "integer", nullable: false),
                    delivery_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "INDIVIDUAL"),
                    last_vote_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vote_notification_cards", x => new { x.content_type, x.content_id });
                    table.CheckConstraint("CK_vote_notification_cards_count_positive", "\"vote_count\" > 0");
                    table.CheckConstraint("CK_vote_notification_cards_delivery_mode", "\"delivery_mode\" IN ('INDIVIDUAL', 'BATCH')");
                });

            migrationBuilder.CreateTable(
                name: "vote_notification_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    window_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vote_notification_events", x => x.id);
                    table.CheckConstraint("CK_vote_notification_events_digest_has_window", "(\"kind\" = 'DIGEST') = (\"window_id\" IS NOT NULL)");
                    table.CheckConstraint("CK_vote_notification_events_kind", "\"kind\" IN ('IMMEDIATE', 'DIGEST', 'UNDECIDED')");
                    table.CheckConstraint("CK_vote_notification_events_status", "\"status\" IN ('PENDING', 'SENDING', 'SUBMITTED', 'SKIPPED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    razorpay_event_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    processing_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_events", x => x.id);
                    table.CheckConstraint("CK_webhook_events_processing_status", "\"processing_status\" IN ('RECEIVED', 'PROCESSING', 'PROCESSED', 'FAILED')");
                    table.CheckConstraint("CK_webhook_events_retry_count", "\"retry_count\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "admin_passwords",
                columns: table => new
                {
                    admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_passwords", x => x.admin_id);
                    table.ForeignKey(
                        name: "FK_admin_passwords_admins_admin_id",
                        column: x => x.admin_id,
                        principalTable: "admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousSessionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreviousHashValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdleExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AbsoluteExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<IPAddress>(type: "inet", nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_sessions_admins_AdminId",
                        column: x => x.AdminId,
                        principalTable: "admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_week_start = table.Column<DateOnly>(type: "date", nullable: true),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    card_theme_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checks", x => x.id);
                    table.CheckConstraint("CK_checks_live_week_start", "(\"live_week_start\" IS NULL) OR (EXTRACT(ISODOW FROM \"live_week_start\") = 1)");
                    table.ForeignKey(
                        name: "FK_checks_card_themes_card_theme_id",
                        column: x => x.card_theme_id,
                        principalTable: "card_themes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("a1f3c0de-0000-4000-8000-000000000001")),
                    PlanActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlanExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "check_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_options", x => x.id);
                    table.UniqueConstraint("AK_check_options_id_check_id", x => new { x.id, x.check_id });
                    table.ForeignKey(
                        name: "FK_check_options_checks_check_id",
                        column: x => x.check_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ask_back_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voter_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    blocked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    asker_message_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    voter_message_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_conversations", x => x.id);
                    table.CheckConstraint("CK_ask_back_conversations_blocked_by", "(\"status\" = 'BLOCKED') = (\"blocked_by_user_id\" IS NOT NULL)");
                    table.CheckConstraint("CK_ask_back_conversations_distinct_users", "\"creator_user_id\" IS NULL OR \"voter_user_id\" IS NULL OR \"creator_user_id\" <> \"voter_user_id\"");
                    table.CheckConstraint("CK_ask_back_conversations_message_counts", "\"asker_message_count\" >= 0 AND \"voter_message_count\" >= 0");
                    table.CheckConstraint("CK_ask_back_conversations_status", "\"status\" IN ('ACTIVE', 'CLOSED', 'BLOCKED')");
                    table.ForeignKey(
                        name: "FK_ask_back_conversations_users_creator_user_id",
                        column: x => x.creator_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ask_back_conversations_users_voter_user_id",
                        column: x => x.voter_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "community_queued_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_queued_questions", x => x.id);
                    table.CheckConstraint("CK_community_queued_questions_question_not_blank", "char_length(btrim(\"question\")) > 0");
                    table.ForeignKey(
                        name: "FK_community_queued_questions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_waitlist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_waitlist", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_waitlist_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    content_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount_in_paise = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    razorpay_order_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    plan_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_orders", x => x.id);
                    table.CheckConstraint("CK_payment_orders_amount_not_negative", "\"amount_in_paise\" >= 0");
                    table.CheckConstraint("CK_payment_orders_card_unlock_shape", "(\"purchase_type\" <> 'CARDUNLOCK') OR (\"plan_id\" IS NOT NULL AND \"content_type\" IN ('POLL', 'CHECK') AND \"content_id\" IS NOT NULL)");
                    table.CheckConstraint("CK_payment_orders_purchase_type", "\"purchase_type\" IN ('USERPLAN', 'CARDUNLOCK')");
                    table.CheckConstraint("CK_payment_orders_status", "\"status\" IN ('CREATED', 'PENDING', 'PAID', 'FAILED', 'CANCELLED', 'EXPIRED')");
                    table.CheckConstraint("CK_payment_orders_user_plan_shape", "(\"purchase_type\" <> 'USERPLAN') OR (\"plan_id\" IS NOT NULL AND \"content_type\" IS NULL AND \"content_id\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_payment_orders_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_orders_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personality_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Draft"),
                    required_votes = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    total_votes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    share_to_community = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_cards", x => x.id);
                    table.CheckConstraint("CK_personality_cards_completed_at_matches_status", "(\"status\" = 'Completed') = (\"completed_at\" IS NOT NULL)");
                    table.CheckConstraint("CK_personality_cards_required_votes_positive", "\"required_votes\" > 0");
                    table.CheckConstraint("CK_personality_cards_status", "\"status\" IN ('Draft', 'Collecting', 'Completed')");
                    table.CheckConstraint("CK_personality_cards_total_votes_in_range", "\"total_votes\" >= 0 AND \"total_votes\" <= \"required_votes\"");
                    table.ForeignKey(
                        name: "FK_personality_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "polls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    share_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "DRAFT"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polls", x => x.id);
                    table.CheckConstraint("CK_polls_content_type", "\"content_type\" IN ('QUESTION', 'IMAGE', 'VIDEO', 'AUDIO')");
                    table.CheckConstraint("CK_polls_question_not_blank", "char_length(btrim(\"question\")) > 0");
                    table.CheckConstraint("CK_polls_share_code_not_blank", "char_length(btrim(\"share_code\")) > 0");
                    table.CheckConstraint("CK_polls_status", "\"status\" IN ('DRAFT', 'ACTIVE', 'CLOSED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "FK_polls_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reason_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_text = table.Column<string>(type: "text", nullable: false),
                    reporter_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reason_reports", x => x.id);
                    table.CheckConstraint("CK_reason_reports_content_type", "\"content_type\" IN ('POLL', 'CHECK')");
                    table.CheckConstraint("CK_reason_reports_status", "\"status\" IN ('PENDING', 'APPROVED', 'REJECTED')");
                    table.ForeignKey(
                        name: "FK_reason_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_activity_days",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_date = table.Column<DateOnly>(type: "date", nullable: false),
                    first_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_activity_days", x => new { x.user_id, x.activity_date });
                    table.ForeignKey(
                        name: "FK_user_activity_days_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_blocks", x => x.id);
                    table.CheckConstraint("CK_user_blocks_distinct_users", "\"blocker_user_id\" <> \"blocked_user_id\"");
                    table.ForeignKey(
                        name: "FK_user_blocks_users_blocked_user_id",
                        column: x => x.blocked_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_blocks_users_blocker_user_id",
                        column: x => x.blocker_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_checks", x => x.id);
                    table.CheckConstraint("CK_user_checks_share_code_not_blank", "char_length(btrim(\"share_code\")) > 0");
                    table.ForeignKey(
                        name: "FK_user_checks_checks_check_id",
                        column: x => x.check_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_checks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_deletion_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scheduled_for = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_deletion_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_deletion_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnonymousName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AllowAskBack = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AnonymousNameKey = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"AnonymousName\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_streaks",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_streak = table.Column<int>(type: "integer", nullable: false),
                    longest_streak = table.Column<int>(type: "integer", nullable: false),
                    last_activity_date = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_streaks", x => x.user_id);
                    table.CheckConstraint("CK_user_streaks_current_positive", "\"current_streak\" >= 1");
                    table.CheckConstraint("CK_user_streaks_longest_at_least_current", "\"longest_streak\" >= \"current_streak\"");
                    table.ForeignKey(
                        name: "FK_user_streaks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ask_back_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    client_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_messages", x => x.id);
                    table.CheckConstraint("CK_ask_back_messages_content_length", "char_length(\"content\") BETWEEN 1 AND 2000");
                    table.ForeignKey(
                        name: "FK_ask_back_messages_ask_back_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ask_back_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ask_back_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ask_back_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_read_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_read_message_created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_ask_back_participants_ask_back_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "ask_back_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ask_back_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "card_tags",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_tags", x => new { x.question_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_card_tags_card_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "card_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_card_tags_community_queued_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "community_queued_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    razorpay_payment_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount_in_paise = table.Column<int>(type: "integer", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    razorpay_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    authorized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_attempts", x => x.id);
                    table.CheckConstraint("CK_payment_attempts_amount_not_negative", "\"amount_in_paise\" >= 0");
                    table.CheckConstraint("CK_payment_attempts_status", "\"status\" IN ('CREATED', 'AUTHORIZED', 'CAPTURED', 'FAILED')");
                    table.ForeignKey(
                        name: "FK_payment_attempts_payment_orders_payment_order_id",
                        column: x => x.payment_order_id,
                        principalTable: "payment_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_plan_entitlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    superseded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_plan_entitlements", x => x.id);
                    table.CheckConstraint("CK_user_plan_entitlements_status", "\"status\" IN ('ACTIVE', 'EXPIRED', 'SUPERSEDED', 'REVOKED')");
                    table.CheckConstraint("CK_user_plan_entitlements_window", "\"expires_at\" > \"starts_at\"");
                    table.ForeignKey(
                        name: "FK_user_plan_entitlements_payment_orders_payment_order_id",
                        column: x => x.payment_order_id,
                        principalTable: "payment_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_plan_entitlements_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_plan_entitlements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personality_card_self_traits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personality_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trait_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_card_self_traits", x => x.id);
                    table.ForeignKey(
                        name: "FK_personality_card_self_traits_personality_cards_personality_~",
                        column: x => x.personality_card_id,
                        principalTable: "personality_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_personality_card_self_traits_personality_traits_trait_id",
                        column: x => x.trait_id,
                        principalTable: "personality_traits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personality_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personality_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voter_guest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_votes", x => x.id);
                    table.CheckConstraint("CK_personality_votes_one_voter", "(\"voter_user_id\" IS NOT NULL AND \"voter_guest_id\" IS NULL) OR (\"voter_user_id\" IS NULL AND \"voter_guest_id\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_personality_votes_guests_voter_guest_id",
                        column: x => x.voter_guest_id,
                        principalTable: "guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_personality_votes_personality_cards_personality_card_id",
                        column: x => x.personality_card_id,
                        principalTable: "personality_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_personality_votes_users_voter_user_id",
                        column: x => x.voter_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_upload_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: true),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bucket_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expected_file_size = table.Column<long>(type: "bigint", nullable: true),
                    actual_file_size = table.Column<long>(type: "bigint", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    selection_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attached_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    purged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_upload_sessions", x => x.id);
                    table.CheckConstraint("CK_media_upload_sessions_actual_size_positive", "\"actual_file_size\" IS NULL OR \"actual_file_size\" > 0");
                    table.CheckConstraint("CK_media_upload_sessions_bucket_not_blank", "char_length(btrim(\"bucket_name\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_duration_non_negative", "\"duration_ms\" IS NULL OR \"duration_ms\" >= 0");
                    table.CheckConstraint("CK_media_upload_sessions_expected_size_positive", "\"expected_file_size\" IS NULL OR \"expected_file_size\" > 0");
                    table.CheckConstraint("CK_media_upload_sessions_failure_reason_not_blank", "\"failure_reason\" IS NULL OR char_length(btrim(\"failure_reason\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_media_type", "\"media_type\" IN ('IMAGE', 'VIDEO', 'AUDIO')");
                    table.CheckConstraint("CK_media_upload_sessions_mime_type_not_blank", "char_length(btrim(\"mime_type\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_object_key_not_blank", "char_length(btrim(\"object_key\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_selection_version_positive", "\"selection_version\" > 0");
                    table.CheckConstraint("CK_media_upload_sessions_status", "\"status\" IN ('PENDING', 'UPLOADING', 'UPLOADED', 'ATTACHED', 'CANCELLED', 'FAILED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "FK_media_upload_sessions_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_media_upload_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "poll_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bucket_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    thumbnail_object_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_media", x => x.id);
                    table.CheckConstraint("CK_poll_media_bucket_not_blank", "char_length(btrim(\"bucket_name\")) > 0");
                    table.CheckConstraint("CK_poll_media_duration_positive", "\"duration_ms\" IS NULL OR \"duration_ms\" > 0");
                    table.CheckConstraint("CK_poll_media_file_size_positive", "\"file_size\" > 0");
                    table.CheckConstraint("CK_poll_media_media_type", "\"media_type\" IN ('IMAGE', 'VIDEO', 'AUDIO')");
                    table.CheckConstraint("CK_poll_media_mime_type_not_blank", "char_length(btrim(\"mime_type\")) > 0");
                    table.CheckConstraint("CK_poll_media_object_key_not_blank", "char_length(btrim(\"object_key\")) > 0");
                    table.CheckConstraint("CK_poll_media_thumbnail_not_blank", "\"thumbnail_object_key\" IS NULL OR char_length(btrim(\"thumbnail_object_key\")) > 0");
                    table.ForeignKey(
                        name: "FK_poll_media_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_options", x => x.id);
                    table.UniqueConstraint("AK_poll_options_id_poll_id", x => new { x.id, x.poll_id });
                    table.CheckConstraint("CK_poll_options_display_order_positive", "\"display_order\" > 0");
                    table.CheckConstraint("CK_poll_options_text_not_blank", "char_length(btrim(\"option_text\")) > 0");
                    table.ForeignKey(
                        name: "FK_poll_options_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funcard_id = table.Column<Guid>(type: "uuid", nullable: true),
                    saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_cards", x => x.id);
                    table.CheckConstraint("CK_saved_cards_card_type_matches_target", "(\"card_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"funcard_id\" IS NULL) OR (\"card_type\" = 'FUNCARD' AND \"funcard_id\" IS NOT NULL AND \"poll_id\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_saved_cards_checks_funcard_id",
                        column: x => x.funcard_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_cards_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "share_codes",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_check_id = table.Column<Guid>(type: "uuid", nullable: true),
                    personality_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_codes", x => x.code);
                    table.CheckConstraint("CK_share_codes_code_not_blank", "char_length(btrim(\"code\")) > 0");
                    table.CheckConstraint("CK_share_codes_content_type", "\"content_type\" IN ('POLL', 'CHECK', 'PERSONALITY')");
                    table.CheckConstraint("CK_share_codes_one_target", "(\"content_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"user_check_id\" IS NULL AND \"personality_card_id\" IS NULL)\nOR\n(\"content_type\" = 'CHECK' AND \"poll_id\" IS NULL AND \"user_check_id\" IS NOT NULL AND \"personality_card_id\" IS NULL)\nOR\n(\"content_type\" = 'PERSONALITY' AND \"poll_id\" IS NULL AND \"user_check_id\" IS NULL AND \"personality_card_id\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_share_codes_personality_cards_personality_card_id",
                        column: x => x.personality_card_id,
                        principalTable: "personality_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_share_codes_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_share_codes_user_checks_user_check_id",
                        column: x => x.user_check_id,
                        principalTable: "user_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_check_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_answer = table.Column<string>(type: "text", nullable: true),
                    voter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "USER"),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_check_votes", x => x.id);
                    table.CheckConstraint("CK_user_check_votes_custom_answer_not_blank", "\"custom_answer\" IS NULL OR char_length(btrim(\"custom_answer\")) > 0");
                    table.CheckConstraint("CK_user_check_votes_one_choice", "(\"check_option_id\" IS NOT NULL AND \"custom_answer\" IS NULL)\nOR\n(\"check_option_id\" IS NULL AND \"custom_answer\" IS NOT NULL)");
                    table.CheckConstraint("CK_user_check_votes_reason_not_blank", "\"reason\" IS NULL OR char_length(btrim(\"reason\")) > 0");
                    table.ForeignKey(
                        name: "FK_user_check_votes_check_options_check_option_id_check_id",
                        columns: x => new { x.check_option_id, x.check_id },
                        principalTable: "check_options",
                        principalColumns: new[] { "id", "check_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_check_votes_user_checks_user_check_id",
                        column: x => x.user_check_id,
                        principalTable: "user_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    razorpay_refund_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    amount_in_paise = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_refunds", x => x.id);
                    table.CheckConstraint("CK_payment_refunds_amount_positive", "\"amount_in_paise\" > 0");
                    table.CheckConstraint("CK_payment_refunds_status", "\"status\" IN ('REQUESTED', 'PROCESSED', 'FAILED')");
                    table.ForeignKey(
                        name: "FK_payment_refunds_payment_attempts_payment_attempt_id",
                        column: x => x.payment_attempt_id,
                        principalTable: "payment_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_refunds_payment_orders_payment_order_id",
                        column: x => x.payment_order_id,
                        principalTable: "payment_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "card_unlocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_plan_entitlement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_unlocks", x => x.id);
                    table.CheckConstraint("CK_card_unlocks_content_type", "\"content_type\" IN ('POLL', 'CHECK')");
                    table.CheckConstraint("CK_card_unlocks_source", "\"source\" IN ('PLAN', 'PURCHASE')");
                    table.ForeignKey(
                        name: "FK_card_unlocks_payment_orders_payment_order_id",
                        column: x => x.payment_order_id,
                        principalTable: "payment_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_card_unlocks_user_plan_entitlements_user_plan_entitlement_id",
                        column: x => x.user_plan_entitlement_id,
                        principalTable: "user_plan_entitlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_card_unlocks_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personality_vote_traits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personality_vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trait_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_vote_traits", x => x.id);
                    table.ForeignKey(
                        name: "FK_personality_vote_traits_personality_traits_trait_id",
                        column: x => x.trait_id,
                        principalTable: "personality_traits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personality_vote_traits_personality_votes_personality_vote_~",
                        column: x => x.personality_vote_id,
                        principalTable: "personality_votes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "USER"),
                    poll_option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_answer = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_votes", x => x.id);
                    table.CheckConstraint("CK_poll_votes_custom_answer_not_blank", "\"custom_answer\" IS NULL OR char_length(btrim(\"custom_answer\")) > 0");
                    table.CheckConstraint("CK_poll_votes_one_choice", "(\"poll_option_id\" IS NOT NULL AND \"custom_answer\" IS NULL)\nOR\n(\"poll_option_id\" IS NULL AND \"custom_answer\" IS NOT NULL)");
                    table.CheckConstraint("CK_poll_votes_reason_not_blank", "\"reason\" IS NULL OR char_length(btrim(\"reason\")) > 0");
                    table.ForeignKey(
                        name: "FK_poll_votes_poll_options_poll_option_id_poll_id",
                        columns: x => new { x.poll_option_id, x.poll_id },
                        principalTable: "poll_options",
                        principalColumns: new[] { "id", "poll_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_poll_votes_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstallationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PushToken = table.Column<string>(type: "text", nullable: true),
                    PushTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeviceModel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppBuild = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NotificationPermission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsPushAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.CheckConstraint("CK_devices_current_session_has_user", "\"CurrentSessionId\" IS NULL OR \"UserId\" IS NOT NULL");
                    table.CheckConstraint("CK_devices_push_allowed_requires_granted", "NOT \"IsPushAllowed\" OR COALESCE(\"NotificationPermission\" = 'Granted', FALSE)");
                    table.CheckConstraint("CK_devices_push_token_hash_matches_token", "(\"PushToken\" IS NULL) = (\"PushTokenHash\" IS NULL)");
                    table.CheckConstraint("CK_devices_single_owner", "NOT (\"UserId\" IS NOT NULL AND \"GuestId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_devices_guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_devices_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                    table.UniqueConstraint("AK_user_sessions_Id_UserId", x => new { x.Id, x.UserId });
                    table.ForeignKey(
                        name: "FK_user_sessions_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "card_categories",
                columns: new[] { "id", "code", "created_at", "is_active", "name", "sort_order" },
                values: new object[,]
                {
                    { new Guid("b1000001-0000-4000-8000-000000000001"), "family", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Family", 1 },
                    { new Guid("b1000001-0000-4000-8000-000000000002"), "money", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Money", 2 },
                    { new Guid("b1000001-0000-4000-8000-000000000003"), "love", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Love", 3 },
                    { new Guid("b1000001-0000-4000-8000-000000000004"), "career", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Career", 4 },
                    { new Guid("b1000001-0000-4000-8000-000000000005"), "friendship", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Friendship", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_admin_id_created_at",
                table: "admin_audit_logs",
                columns: new[] { "admin_id", "created_at" },
                filter: "\"admin_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_created_at",
                table: "admin_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_logs_event_type_created_at",
                table: "admin_audit_logs",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AbsoluteExpiresAt",
                table: "admin_sessions",
                column: "AbsoluteExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminId_RevokedAt",
                table: "admin_sessions",
                columns: new[] { "AdminId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_PreviousSessionHash",
                table: "admin_sessions",
                column: "PreviousSessionHash");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_SessionHash",
                table: "admin_sessions",
                column: "SessionHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Email",
                table: "admins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_conversations_creator_activity",
                table: "ask_back_conversations",
                columns: new[] { "creator_user_id", "last_activity_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_conversations_vote_id",
                table: "ask_back_conversations",
                column: "vote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_conversations_voter_activity",
                table: "ask_back_conversations",
                columns: new[] { "voter_user_id", "last_activity_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_messages_history",
                table: "ask_back_messages",
                columns: new[] { "conversation_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_messages_idempotency",
                table: "ask_back_messages",
                columns: new[] { "conversation_id", "sender_user_id", "client_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_messages_sender_user_id",
                table: "ask_back_messages",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_outbox_created_at",
                table: "ask_back_outbox",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_outbox_due",
                table: "ask_back_outbox",
                column: "next_attempt_at",
                filter: "\"status\" IN ('PENDING', 'PROCESSING')");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_outbox_message_id",
                table: "ask_back_outbox",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_outbox_notification_id",
                table: "ask_back_outbox",
                column: "notification_id",
                filter: "\"notification_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_participants_conversation_user",
                table: "ask_back_participants",
                columns: new[] { "conversation_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_participants_user_id",
                table: "ask_back_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_reports_conversation_id",
                table: "ask_back_reports",
                column: "conversation_id");

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
                name: "IX_card_categories_code",
                table: "card_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_card_categories_sort_order",
                table: "card_categories",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "IX_card_tags_category_id",
                table: "card_tags",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_card_themes_is_active",
                table: "card_themes",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_card_themes_kind_name",
                table: "card_themes",
                columns: new[] { "kind", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_card_unlocks_owner_card",
                table: "card_unlocks",
                columns: new[] { "owner_user_id", "content_type", "content_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_card_unlocks_owner_source_created",
                table: "card_unlocks",
                columns: new[] { "owner_user_id", "source", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_card_unlocks_payment_order_id",
                table: "card_unlocks",
                column: "payment_order_id",
                filter: "\"payment_order_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_card_unlocks_user_plan_entitlement_id",
                table: "card_unlocks",
                column: "user_plan_entitlement_id",
                filter: "\"user_plan_entitlement_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_check_options_check_id_sort_order",
                table: "check_options",
                columns: new[] { "check_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_checks_card_theme_id",
                table: "checks",
                column: "card_theme_id");

            migrationBuilder.CreateIndex(
                name: "IX_checks_live_week_start_created_at",
                table: "checks",
                columns: new[] { "live_week_start", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_community_queued_questions_user_id_created_at",
                table: "community_queued_questions",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_community_waitlist_notified_at_created_at",
                table: "community_waitlist",
                columns: new[] { "notified_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_community_waitlist_user_id",
                table: "community_waitlist",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_audit_logs_content",
                table: "content_audit_logs",
                columns: new[] { "content_type", "content_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_content_audit_logs_created_at",
                table: "content_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_content_audit_logs_event_type_created_at",
                table: "content_audit_logs",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CurrentSessionId",
                table: "devices",
                column: "CurrentSessionId",
                unique: true,
                filter: "\"CurrentSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_devices_CurrentSessionId_UserId",
                table: "devices",
                columns: new[] { "CurrentSessionId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_GuestId",
                table: "devices",
                column: "GuestId",
                filter: "\"GuestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_InstallationId_Platform",
                table: "devices",
                columns: new[] { "InstallationId", "Platform" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PushTokenHash",
                table: "devices",
                column: "PushTokenHash",
                unique: true,
                filter: "\"PushTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_audit_logs_created_at",
                table: "entitlement_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_audit_logs_event_type_created_at",
                table: "entitlement_audit_logs",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_audit_logs_user_id_created_at",
                table: "entitlement_audit_logs",
                columns: new[] { "user_id", "created_at" },
                filter: "\"user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_ExpiresAt",
                table: "guests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_SessionTokenHash",
                table: "guests",
                column: "SessionTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_object_key",
                table: "media_upload_sessions",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_poll_id",
                table: "media_upload_sessions",
                column: "poll_id",
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_user_id_status_expires_at",
                table: "media_upload_sessions",
                columns: new[] { "user_id", "status", "expires_at" });

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
                name: "IX_Notifications_UserId_CreatedAt",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_payment_order_id",
                table: "payment_attempts",
                column: "payment_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_razorpay_payment_id",
                table: "payment_attempts",
                column: "razorpay_payment_id",
                unique: true,
                filter: "\"razorpay_payment_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_logs_correlation_id",
                table: "payment_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_logs_created_at",
                table: "payment_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_logs_event_type_created_at",
                table: "payment_logs",
                columns: new[] { "event_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_logs_payment_order_id",
                table: "payment_logs",
                column: "payment_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_logs_user_id",
                table: "payment_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_plan_id",
                table: "payment_orders",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_razorpay_order_id",
                table: "payment_orders",
                column: "razorpay_order_id",
                unique: true,
                filter: "\"razorpay_order_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_status",
                table: "payment_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_user_created_at",
                table: "payment_orders",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_orders_user_idempotency",
                table: "payment_orders",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_created_at",
                table: "payment_refunds",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_payment_attempt_id",
                table: "payment_refunds",
                column: "payment_attempt_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_payment_order_id",
                table: "payment_refunds",
                column: "payment_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_razorpay_refund_id",
                table: "payment_refunds",
                column: "razorpay_refund_id",
                unique: true,
                filter: "\"razorpay_refund_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personality_card_self_traits_card_id_trait_id",
                table: "personality_card_self_traits",
                columns: new[] { "personality_card_id", "trait_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_card_self_traits_trait_id",
                table: "personality_card_self_traits",
                column: "trait_id");

            migrationBuilder.CreateIndex(
                name: "IX_personality_cards_share_code",
                table: "personality_cards",
                column: "share_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_cards_user_id",
                table: "personality_cards",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_traits_display_order",
                table: "personality_traits",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "IX_personality_traits_name",
                table: "personality_traits",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_vote_traits_trait_id",
                table: "personality_vote_traits",
                column: "trait_id");

            migrationBuilder.CreateIndex(
                name: "IX_personality_vote_traits_vote_id_trait_id",
                table: "personality_vote_traits",
                columns: new[] { "personality_vote_id", "trait_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_card_id_voter_guest_id",
                table: "personality_votes",
                columns: new[] { "personality_card_id", "voter_guest_id" },
                unique: true,
                filter: "\"voter_guest_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_card_id_voter_user_id",
                table: "personality_votes",
                columns: new[] { "personality_card_id", "voter_user_id" },
                unique: true,
                filter: "\"voter_user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_voter_guest_id",
                table: "personality_votes",
                column: "voter_guest_id",
                filter: "\"voter_guest_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_voter_user_id",
                table: "personality_votes",
                column: "voter_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_plans_code",
                table: "plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plans_sort_order",
                table: "plans",
                column: "sort_order",
                unique: true);

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
                name: "IX_poll_media_poll_id",
                table: "poll_media",
                column: "poll_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_options_poll_id_display_order",
                table: "poll_options",
                columns: new[] { "poll_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_options_poll_id_option_text",
                table: "poll_options",
                columns: new[] { "poll_id", "option_text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_poll_id_voter_id",
                table: "poll_votes",
                columns: new[] { "poll_id", "voter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_poll_option_id",
                table: "poll_votes",
                column: "poll_option_id",
                filter: "\"poll_option_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_poll_option_id_poll_id",
                table: "poll_votes",
                columns: new[] { "poll_option_id", "poll_id" });

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_voter_id",
                table: "poll_votes",
                column: "voter_id");

            migrationBuilder.CreateIndex(
                name: "IX_polls_active_created_at",
                table: "polls",
                column: "created_at",
                descending: new bool[0],
                filter: "\"status\" = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_polls_created_by_created_at",
                table: "polls",
                columns: new[] { "created_by", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_polls_expires_at",
                table: "polls",
                column: "expires_at",
                filter: "\"expires_at\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_polls_share_code",
                table: "polls",
                column: "share_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_content",
                table: "reason_reports",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_created_at",
                table: "reason_reports",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_reporter_id",
                table: "reason_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_status_created_at",
                table: "reason_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_vote_id",
                table: "reason_reports",
                column: "vote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_funcard_id",
                table: "saved_cards",
                column: "funcard_id",
                filter: "\"funcard_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_poll_id",
                table: "saved_cards",
                column: "poll_id",
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_funcard",
                table: "saved_cards",
                columns: new[] { "user_id", "funcard_id" },
                unique: true,
                filter: "\"funcard_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_poll",
                table: "saved_cards",
                columns: new[] { "user_id", "poll_id" },
                unique: true,
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_saved_at",
                table: "saved_cards",
                columns: new[] { "user_id", "saved_at" });

            migrationBuilder.CreateIndex(
                name: "IX_share_codes_personality_card_id",
                table: "share_codes",
                column: "personality_card_id",
                unique: true,
                filter: "\"personality_card_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_share_codes_poll_id",
                table: "share_codes",
                column: "poll_id",
                unique: true,
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_share_codes_user_check_id",
                table: "share_codes",
                column: "user_check_id",
                unique: true,
                filter: "\"user_check_id\" IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_user_blocks_blocked_user_id",
                table: "user_blocks",
                column: "blocked_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_blocks_pair",
                table: "user_blocks",
                columns: new[] { "blocker_user_id", "blocked_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_check_option_id",
                table: "user_check_votes",
                column: "check_option_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_check_option_id_check_id",
                table: "user_check_votes",
                columns: new[] { "check_option_id", "check_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_user_check_id_voter_id",
                table: "user_check_votes",
                columns: new[] { "user_check_id", "voter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_voter_id",
                table: "user_check_votes",
                column: "voter_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_checks_check_id",
                table: "user_checks",
                column: "check_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_checks_share_code",
                table: "user_checks",
                column: "share_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_checks_user_id_check_id",
                table: "user_checks",
                columns: new[] { "user_id", "check_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_deletion_requests_scheduled_for",
                table: "user_deletion_requests",
                column: "scheduled_for");

            migrationBuilder.CreateIndex(
                name: "IX_user_deletion_requests_user_id",
                table: "user_deletion_requests",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_plan_entitlements_expires_at",
                table: "user_plan_entitlements",
                column: "expires_at",
                filter: "\"status\" = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_user_plan_entitlements_one_active",
                table: "user_plan_entitlements",
                column: "user_id",
                unique: true,
                filter: "\"status\" = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "IX_user_plan_entitlements_payment_order_id",
                table: "user_plan_entitlements",
                column: "payment_order_id",
                unique: true,
                filter: "\"payment_order_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_plan_entitlements_plan_id",
                table: "user_plan_entitlements",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_plan_entitlements_user_status",
                table: "user_plan_entitlements",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AnonymousName",
                table: "user_profiles",
                column: "AnonymousNameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "user_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_DeviceId",
                table: "user_sessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_RefreshTokenHash",
                table: "user_sessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "user_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                table: "users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PlanId",
                table: "users",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_vote_digest_windows_due",
                table: "vote_digest_windows",
                column: "ends_at",
                filter: "\"status\" IN ('OPEN', 'DUE', 'PROCESSING')");

            migrationBuilder.CreateIndex(
                name: "IX_vote_digest_windows_open_per_card",
                table: "vote_digest_windows",
                columns: new[] { "content_type", "content_id" },
                unique: true,
                filter: "\"status\" = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_card_sequence",
                table: "vote_notification_events",
                columns: new[] { "content_type", "content_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_content_type_vote_id",
                table: "vote_notification_events",
                columns: new[] { "content_type", "vote_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_created_at",
                table: "vote_notification_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_due",
                table: "vote_notification_events",
                column: "next_attempt_at",
                filter: "\"kind\" = 'IMMEDIATE' AND \"status\" IN ('PENDING', 'SENDING')");

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_undecided",
                table: "vote_notification_events",
                columns: new[] { "kind", "created_at" },
                filter: "\"kind\" = 'UNDECIDED' AND \"status\" IN ('PENDING', 'SENDING')");

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_window_id_status",
                table: "vote_notification_events",
                columns: new[] { "window_id", "status" },
                filter: "\"window_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_razorpay_event_id",
                table: "webhook_events",
                column: "razorpay_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_status_received_at",
                table: "webhook_events",
                columns: new[] { "processing_status", "received_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_devices_user_sessions_CurrentSessionId_UserId",
                table: "devices",
                columns: new[] { "CurrentSessionId", "UserId" },
                principalTable: "user_sessions",
                principalColumns: new[] { "Id", "UserId" },
                onDelete: ReferentialAction.SetNull);

            // Catalog seeds (previously lived in incremental migrations).
            migrationBuilder.Sql("""
                INSERT INTO plans (
                    id, code, name, sort_order, price_minor, currency, is_active,
                    duration_days, unlock_tickets, ask_back_message_limit, created_at, updated_at
                ) VALUES
                    ('a1f3c0de-0000-4000-8000-000000000001', 'free',      'Free',      1,     0, 'INR', true, NULL, 0,    1,    now(), now()),
                    ('a1f3c0de-0000-4000-8000-000000000004', 'this_card', 'This card', 2,  4900, 'INR', true, NULL, 1,    1,    now(), now()),
                    ('a1f3c0de-0000-4000-8000-000000000002', 'yakku_lite','Yakku Lite',3,  9900, 'INR', true, 7,    3,    3,    now(), now()),
                    ('a1f3c0de-0000-4000-8000-000000000003', 'yakku_love','Yakku Love',4, 29900, 'INR', true, 30,   NULL, NULL, now(), now())
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO personality_traits (id, name, is_active, display_order) VALUES
                    ('b2e4d1a0-0000-4000-8000-000000000001', 'Confident',   true, 1),
                    ('b2e4d1a0-0000-4000-8000-000000000002', 'Introverted', true, 2),
                    ('b2e4d1a0-0000-4000-8000-000000000003', 'Creative',    true, 3),
                    ('b2e4d1a0-0000-4000-8000-000000000004', 'Empathetic',  true, 4),
                    ('b2e4d1a0-0000-4000-8000-000000000005', 'Ambitious',   true, 5),
                    ('b2e4d1a0-0000-4000-8000-000000000006', 'Humorous',    true, 6),
                    ('b2e4d1a0-0000-4000-8000-000000000007', 'Intelligent', true, 7),
                    ('b2e4d1a0-0000-4000-8000-000000000008', 'Calm',        true, 8),
                    ('b2e4d1a0-0000-4000-8000-000000000009', 'Adventurous', true, 9),
                    ('b2e4d1a0-0000-4000-8000-000000000010', 'Reliable',    true, 10)
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO card_themes (id, kind, name, background_color, label_color, tag, is_active) VALUES
                    (gen_random_uuid(), 'CHECK',       'Mirror Check',           '#FFE0E8', '#993556', 'how people see you', true),
                    (gen_random_uuid(), 'CHECK',       'Vibe Check',             '#ECE3FF', '#4A3ABC', 'energy and personality', true),
                    (gen_random_uuid(), 'CHECK',       'Trust Check',            '#DDF3E4', '#245C3A', 'reliability and secrets', true),
                    (gen_random_uuid(), 'FUN',         'Just for fun',           '#FFEFC2', '#6B4B00', 'Movies, jail, silly ones', true),
                    (gen_random_uuid(), 'FUN',         'Question worth asking',  '#FFAA70', '#7A2E00', NULL, true),
                    (gen_random_uuid(), 'FUN',         'Emotional',              '#86CCFF', '#0B3A66', NULL, true),
                    (gen_random_uuid(), 'PERSONALITY', 'Personality',            '#D02E62', '#FFE0E8', 'The 10-trait card', true),
                    (gen_random_uuid(), 'POLL',        'Your Question',          '#E8F6C3', '#4B5E12', 'Default for question you write', true)
                ON CONFLICT (kind, name) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_users_UserId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_user_sessions_users_UserId",
                table: "user_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_guests_GuestId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_user_sessions_CurrentSessionId_UserId",
                table: "devices");

            migrationBuilder.DropTable(
                name: "admin_audit_logs");

            migrationBuilder.DropTable(
                name: "admin_passwords");

            migrationBuilder.DropTable(
                name: "admin_sessions");

            migrationBuilder.DropTable(
                name: "ask_back_messages");

            migrationBuilder.DropTable(
                name: "ask_back_outbox");

            migrationBuilder.DropTable(
                name: "ask_back_participants");

            migrationBuilder.DropTable(
                name: "ask_back_reports");

            migrationBuilder.DropTable(
                name: "authentication_audit_logs");

            migrationBuilder.DropTable(
                name: "card_tags");

            migrationBuilder.DropTable(
                name: "card_unlocks");

            migrationBuilder.DropTable(
                name: "community_waitlist");

            migrationBuilder.DropTable(
                name: "content_audit_logs");

            migrationBuilder.DropTable(
                name: "entitlement_audit_logs");

            migrationBuilder.DropTable(
                name: "media_upload_sessions");

            migrationBuilder.DropTable(
                name: "notification_audit_logs");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "payment_logs");

            migrationBuilder.DropTable(
                name: "payment_refunds");

            migrationBuilder.DropTable(
                name: "personality_card_self_traits");

            migrationBuilder.DropTable(
                name: "personality_vote_traits");

            migrationBuilder.DropTable(
                name: "poll_audit_logs");

            migrationBuilder.DropTable(
                name: "poll_media");

            migrationBuilder.DropTable(
                name: "poll_votes");

            migrationBuilder.DropTable(
                name: "reason_reports");

            migrationBuilder.DropTable(
                name: "saved_cards");

            migrationBuilder.DropTable(
                name: "share_codes");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "user_activity_days");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "user_check_votes");

            migrationBuilder.DropTable(
                name: "user_deletion_requests");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "user_streaks");

            migrationBuilder.DropTable(
                name: "vote_digest_windows");

            migrationBuilder.DropTable(
                name: "vote_notification_cards");

            migrationBuilder.DropTable(
                name: "vote_notification_events");

            migrationBuilder.DropTable(
                name: "webhook_events");

            migrationBuilder.DropTable(
                name: "admins");

            migrationBuilder.DropTable(
                name: "ask_back_conversations");

            migrationBuilder.DropTable(
                name: "card_categories");

            migrationBuilder.DropTable(
                name: "community_queued_questions");

            migrationBuilder.DropTable(
                name: "user_plan_entitlements");

            migrationBuilder.DropTable(
                name: "payment_attempts");

            migrationBuilder.DropTable(
                name: "personality_traits");

            migrationBuilder.DropTable(
                name: "personality_votes");

            migrationBuilder.DropTable(
                name: "poll_options");

            migrationBuilder.DropTable(
                name: "check_options");

            migrationBuilder.DropTable(
                name: "user_checks");

            migrationBuilder.DropTable(
                name: "payment_orders");

            migrationBuilder.DropTable(
                name: "personality_cards");

            migrationBuilder.DropTable(
                name: "polls");

            migrationBuilder.DropTable(
                name: "checks");

            migrationBuilder.DropTable(
                name: "card_themes");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropTable(
                name: "guests");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "devices");
        }
    }
}
