using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AskBackMessageDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ask_back_message_delivery_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ask_back_message_delivery_tracking", x => x.id);
                    table.CheckConstraint("CK_ask_back_message_delivery_tracking_delivered_at", "(\"status\" = 'DELIVERED') = (\"delivered_at\" IS NOT NULL)");
                    table.CheckConstraint("CK_ask_back_message_delivery_tracking_status", "\"status\" IN ('PENDING', 'SENT', 'DELIVERED', 'FAILED', 'TIMEDOUT', 'NOACKCONNECTION')");
                    table.ForeignKey(
                        name: "FK_ask_back_message_delivery_tracking_ask_back_messages_messag~",
                        column: x => x.message_id,
                        principalTable: "ask_back_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ask_back_message_delivery_tracking_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_delivery_message_recipient",
                table: "ask_back_message_delivery_tracking",
                columns: new[] { "message_id", "recipient_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_delivery_sent_at",
                table: "ask_back_message_delivery_tracking",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_delivery_waiting",
                table: "ask_back_message_delivery_tracking",
                columns: new[] { "status", "sent_at" },
                filter: "\"status\" IN ('PENDING', 'SENT')");

            migrationBuilder.CreateIndex(
                name: "IX_ask_back_message_delivery_tracking_recipient_user_id",
                table: "ask_back_message_delivery_tracking",
                column: "recipient_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ask_back_message_delivery_tracking");
        }
    }
}
