using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AskBackPlansUnlocksAndTurns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_muted",
                table: "ask_back_participants");

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanActivatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanExpiresAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ask_back_message_limit",
                table: "plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "duration_days",
                table: "plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "unlock_tickets",
                table: "plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "asker_message_count",
                table: "ask_back_conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "voter_message_count",
                table: "ask_back_conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "card_unlocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_unlocks", x => x.id);
                    table.CheckConstraint("CK_card_unlocks_content_type", "\"content_type\" IN ('POLL', 'CHECK')");
                    table.CheckConstraint("CK_card_unlocks_source", "\"source\" IN ('PLAN', 'PURCHASE')");
                    table.ForeignKey(
                        name: "FK_card_unlocks_users_owner_user_id",
                        column: x => x.owner_user_id,
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

            // Plan perks, by the plans' fixed ids. free: no expiry, no tickets, one question per reason.
            // yakku_lite: 7 days, 3 unlock tickets, 3 questions per reason. yakku_love: 30 days, unlimited (null).
            migrationBuilder.Sql(
                """
                UPDATE plans SET duration_days = NULL, unlock_tickets = 0, ask_back_message_limit = 1
                WHERE id = 'a1f3c0de-0000-4000-8000-000000000001';
                UPDATE plans SET duration_days = 7, unlock_tickets = 3, ask_back_message_limit = 3
                WHERE id = 'a1f3c0de-0000-4000-8000-000000000002';
                UPDATE plans SET duration_days = 30, unlock_tickets = NULL, ask_back_message_limit = NULL
                WHERE id = 'a1f3c0de-0000-4000-8000-000000000003';
                """);

            // Cards that already have a conversation keep working: each becomes a single-card (purchase) unlock for its
            // owner, which never expires. The owner is the asker; a conversation whose asker is gone has no owner left.
            migrationBuilder.Sql(
                """
                INSERT INTO card_unlocks (id, owner_user_id, content_type, content_id, source, created_at)
                SELECT DISTINCT ON (c.content_type, c.content_id)
                       gen_random_uuid(), c.creator_user_id, c.content_type, c.content_id, 'PURCHASE', now()
                FROM ask_back_conversations c
                WHERE c.creator_user_id IS NOT NULL
                  AND EXISTS (SELECT 1 FROM users u WHERE u."Id" = c.creator_user_id)
                ORDER BY c.content_type, c.content_id, c.created_at;
                """);

            // The turn counters of existing conversations, from the messages each side already sent.
            migrationBuilder.Sql(
                """
                UPDATE ask_back_conversations c
                SET asker_message_count = (
                        SELECT COUNT(*) FROM ask_back_messages m
                        WHERE m.conversation_id = c.id AND m.sender_user_id = c.creator_user_id),
                    voter_message_count = (
                        SELECT COUNT(*) FROM ask_back_messages m
                        WHERE m.conversation_id = c.id AND m.sender_user_id = c.voter_user_id);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_plans_ask_back_message_limit",
                table: "plans",
                sql: "\"ask_back_message_limit\" IS NULL OR \"ask_back_message_limit\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_plans_duration_days",
                table: "plans",
                sql: "\"duration_days\" IS NULL OR \"duration_days\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_plans_unlock_tickets",
                table: "plans",
                sql: "\"unlock_tickets\" IS NULL OR \"unlock_tickets\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ask_back_conversations_message_counts",
                table: "ask_back_conversations",
                sql: "\"asker_message_count\" >= 0 AND \"voter_message_count\" >= 0");

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
                name: "IX_user_blocks_blocked_user_id",
                table: "user_blocks",
                column: "blocked_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_blocks_pair",
                table: "user_blocks",
                columns: new[] { "blocker_user_id", "blocked_user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "card_unlocks");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_plans_ask_back_message_limit",
                table: "plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_plans_duration_days",
                table: "plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_plans_unlock_tickets",
                table: "plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ask_back_conversations_message_counts",
                table: "ask_back_conversations");

            migrationBuilder.DropColumn(
                name: "PlanActivatedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ask_back_message_limit",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "duration_days",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "unlock_tickets",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "asker_message_count",
                table: "ask_back_conversations");

            migrationBuilder.DropColumn(
                name: "voter_message_count",
                table: "ask_back_conversations");

            migrationBuilder.AddColumn<bool>(
                name: "is_muted",
                table: "ask_back_participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
