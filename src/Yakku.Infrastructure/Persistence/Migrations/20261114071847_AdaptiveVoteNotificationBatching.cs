using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdaptiveVoteNotificationBatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_vote_notification_events_kind",
                table: "vote_notification_events");

            migrationBuilder.AddColumn<string>(
                name: "provider_message_id",
                table: "vote_notification_events",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_mode",
                table: "vote_notification_cards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "INDIVIDUAL");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_vote_at",
                table: "vote_notification_cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "vote_notification_cards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "claimed_at",
                table: "vote_digest_windows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "generation",
                table: "vote_digest_windows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_vote_at",
                table: "vote_digest_windows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "max_flush_at",
                table: "vote_digest_windows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_message_id",
                table: "vote_digest_windows",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_card_sequence",
                table: "vote_notification_events",
                columns: new[] { "content_type", "content_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_vote_notification_events_undecided",
                table: "vote_notification_events",
                columns: new[] { "kind", "created_at" },
                filter: "\"kind\" = 'UNDECIDED' AND \"status\" IN ('PENDING', 'SENDING')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_vote_notification_events_kind",
                table: "vote_notification_events",
                sql: "\"kind\" IN ('IMMEDIATE', 'DIGEST', 'UNDECIDED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_vote_notification_cards_delivery_mode",
                table: "vote_notification_cards",
                sql: "\"delivery_mode\" IN ('INDIVIDUAL', 'BATCH')");

            // Windows opened by the previous fixed-length batching were meant to close at ends_at and carry no
            // generation or maximum wait. Hand them to the sender now (DUE) so their votes go out in one push each
            // instead of waiting for the new schedule, which does not know them. Idempotent.
            migrationBuilder.Sql(
                "UPDATE vote_digest_windows SET status = 'DUE', next_attempt_at = NULL WHERE status = 'OPEN' AND generation = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vote_notification_events_card_sequence",
                table: "vote_notification_events");

            migrationBuilder.DropIndex(
                name: "IX_vote_notification_events_undecided",
                table: "vote_notification_events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_vote_notification_events_kind",
                table: "vote_notification_events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_vote_notification_cards_delivery_mode",
                table: "vote_notification_cards");

            migrationBuilder.DropColumn(
                name: "provider_message_id",
                table: "vote_notification_events");

            migrationBuilder.DropColumn(
                name: "delivery_mode",
                table: "vote_notification_cards");

            migrationBuilder.DropColumn(
                name: "last_vote_at",
                table: "vote_notification_cards");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "vote_notification_cards");

            migrationBuilder.DropColumn(
                name: "claimed_at",
                table: "vote_digest_windows");

            migrationBuilder.DropColumn(
                name: "generation",
                table: "vote_digest_windows");

            migrationBuilder.DropColumn(
                name: "last_vote_at",
                table: "vote_digest_windows");

            migrationBuilder.DropColumn(
                name: "max_flush_at",
                table: "vote_digest_windows");

            migrationBuilder.DropColumn(
                name: "provider_message_id",
                table: "vote_digest_windows");

            migrationBuilder.AddCheckConstraint(
                name: "CK_vote_notification_events_kind",
                table: "vote_notification_events",
                sql: "\"kind\" IN ('IMMEDIATE', 'DIGEST')");
        }
    }
}
