using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAskBack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_conversations", x => x.id);
                    table.CheckConstraint("CK_ask_back_conversations_blocked_by", "(\"status\" = 'BLOCKED') = (\"blocked_by_user_id\" IS NOT NULL)");
                    table.CheckConstraint("CK_ask_back_conversations_distinct_users", "\"creator_user_id\" IS NULL OR \"voter_user_id\" IS NULL OR \"creator_user_id\" <> \"voter_user_id\"");
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
                name: "ask_back_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    is_muted = table.Column<bool>(type: "boolean", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ask_back_messages");

            migrationBuilder.DropTable(
                name: "ask_back_outbox");

            migrationBuilder.DropTable(
                name: "ask_back_participants");

            migrationBuilder.DropTable(
                name: "ask_back_reports");

            migrationBuilder.DropTable(
                name: "ask_back_conversations");
        }
    }
}
