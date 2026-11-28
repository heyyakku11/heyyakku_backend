using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationTopUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_orders_purchase_type",
                table: "payment_orders");

            migrationBuilder.AddColumn<Guid>(
                name: "conversation_id",
                table: "payment_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "top_up_granted",
                table: "ask_back_conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "top_up_used",
                table: "ask_back_conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ask_back_conversation_top_ups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credits = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_conversation_top_ups", x => x.id);
                    table.CheckConstraint("CK_ask_back_conversation_top_ups_credits_positive", "\"credits\" > 0");
                    table.ForeignKey(
                        name: "FK_ask_back_conversation_top_ups_ask_back_conversations_conver~",
                        column: x => x.conversation_id,
                        principalTable: "ask_back_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ask_back_conversation_top_ups_payment_orders_payment_order_~",
                        column: x => x.payment_order_id,
                        principalTable: "payment_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_orders_conversation_id_only_for_top_up",
                table: "payment_orders",
                sql: "\"conversation_id\" IS NULL OR \"purchase_type\" = 'CONVERSATIONTOPUP'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_orders_conversation_top_up_shape",
                table: "payment_orders",
                sql: "(\"purchase_type\" <> 'CONVERSATIONTOPUP') OR (\"plan_id\" IS NOT NULL AND \"content_type\" IS NULL AND \"content_id\" IS NULL AND \"conversation_id\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_orders_purchase_type",
                table: "payment_orders",
                sql: "\"purchase_type\" IN ('USERPLAN', 'CARDUNLOCK', 'CONVERSATIONTOPUP')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ask_back_conversations_top_up_counts",
                table: "ask_back_conversations",
                sql: "\"top_up_granted\" >= 0 AND \"top_up_used\" >= 0 AND \"top_up_used\" <= \"top_up_granted\"");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_conversation_top_ups_conversation_id",
                table: "ask_back_conversation_top_ups",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_conversation_top_ups_payment_order_id",
                table: "ask_back_conversation_top_ups",
                column: "payment_order_id",
                unique: true);

            // Conversation top-up catalog: one-off question packs, no duration (they never expire and never change the
            // user's plan).
            migrationBuilder.Sql("""
                INSERT INTO plans (
                    id, code, name, sort_order, price_minor, currency, is_active,
                    duration_days, unlock_tickets, ask_back_message_limit, created_at, updated_at
                ) VALUES
                    ('a1f3c0de-0000-4000-8000-000000000005', 'top_up_19', 'Top up 19', 5, 1900, 'INR', true, NULL, 0, 3, now(), now()),
                    ('a1f3c0de-0000-4000-8000-000000000006', 'top_up_29', 'Top up 29', 6, 2900, 'INR', true, NULL, 0, 5, now(), now())
                ON CONFLICT (id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM plans
                WHERE id IN ('a1f3c0de-0000-4000-8000-000000000005', 'a1f3c0de-0000-4000-8000-000000000006');
                """);

            migrationBuilder.DropTable(
                name: "ask_back_conversation_top_ups");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_orders_conversation_id_only_for_top_up",
                table: "payment_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_orders_conversation_top_up_shape",
                table: "payment_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_payment_orders_purchase_type",
                table: "payment_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ask_back_conversations_top_up_counts",
                table: "ask_back_conversations");

            migrationBuilder.DropColumn(
                name: "conversation_id",
                table: "payment_orders");

            migrationBuilder.DropColumn(
                name: "top_up_granted",
                table: "ask_back_conversations");

            migrationBuilder.DropColumn(
                name: "top_up_used",
                table: "ask_back_conversations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_payment_orders_purchase_type",
                table: "payment_orders",
                sql: "\"purchase_type\" IN ('USERPLAN', 'CARDUNLOCK')");
        }
    }
}
