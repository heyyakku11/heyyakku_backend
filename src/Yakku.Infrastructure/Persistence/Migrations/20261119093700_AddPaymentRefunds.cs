using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_refunds");
        }
    }
}
