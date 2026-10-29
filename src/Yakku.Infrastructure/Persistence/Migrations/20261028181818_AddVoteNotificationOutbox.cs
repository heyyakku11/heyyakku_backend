using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoteNotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    vote_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vote_notification_cards", x => new { x.content_type, x.content_id });
                    table.CheckConstraint("CK_vote_notification_cards_count_positive", "\"vote_count\" > 0");
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
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vote_notification_events", x => x.id);
                    table.CheckConstraint("CK_vote_notification_events_digest_has_window", "(\"kind\" = 'DIGEST') = (\"window_id\" IS NOT NULL)");
                    table.CheckConstraint("CK_vote_notification_events_kind", "\"kind\" IN ('IMMEDIATE', 'DIGEST')");
                    table.CheckConstraint("CK_vote_notification_events_status", "\"status\" IN ('PENDING', 'SENDING', 'SUBMITTED', 'SKIPPED', 'FAILED')");
                });

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
                name: "IX_vote_notification_events_window_id_status",
                table: "vote_notification_events",
                columns: new[] { "window_id", "status" },
                filter: "\"window_id\" IS NOT NULL");

            // Existing cards start with the number of votes that already notified (the asker's own vote does not),
            // so they do not get another three immediate pushes. No events or windows are created for old votes.
            migrationBuilder.Sql(
                """
                INSERT INTO vote_notification_cards (content_type, content_id, owner_id, vote_count)
                SELECT 'POLL', p.id, p.created_by, COUNT(*)
                FROM poll_votes v
                JOIN polls p ON p.id = v.poll_id
                WHERE v.voter_id <> p.created_by
                GROUP BY p.id, p.created_by;
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO vote_notification_cards (content_type, content_id, owner_id, vote_count)
                SELECT 'CHECK', c.id, c.user_id, COUNT(*)
                FROM user_check_votes v
                JOIN user_checks c ON c.id = v.user_check_id
                WHERE v.voter_id <> c.user_id
                GROUP BY c.id, c.user_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vote_digest_windows");

            migrationBuilder.DropTable(
                name: "vote_notification_cards");

            migrationBuilder.DropTable(
                name: "vote_notification_events");
        }
    }
}
