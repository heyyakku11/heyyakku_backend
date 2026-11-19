using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "payment_order_id",
                table: "card_unlocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_plan_entitlement_id",
                table: "card_unlocks",
                type: "uuid",
                nullable: true);

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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_logs", x => x.id);
                    table.CheckConstraint("CK_payment_logs_message_not_blank", "char_length(btrim(\"message\")) > 0");
                    table.CheckConstraint("CK_payment_logs_status", "\"status\" IN ('INFORMATION', 'SUCCESS', 'WARNING', 'FAILURE')");
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
                name: "IX_webhook_events_razorpay_event_id",
                table: "webhook_events",
                column: "razorpay_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_events_status_received_at",
                table: "webhook_events",
                columns: new[] { "processing_status", "received_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_card_unlocks_payment_orders_payment_order_id",
                table: "card_unlocks",
                column: "payment_order_id",
                principalTable: "payment_orders",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_card_unlocks_user_plan_entitlements_user_plan_entitlement_id",
                table: "card_unlocks",
                column: "user_plan_entitlement_id",
                principalTable: "user_plan_entitlements",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_card_unlocks_payment_orders_payment_order_id",
                table: "card_unlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_card_unlocks_user_plan_entitlements_user_plan_entitlement_id",
                table: "card_unlocks");

            migrationBuilder.DropTable(
                name: "payment_attempts");

            migrationBuilder.DropTable(
                name: "payment_logs");

            migrationBuilder.DropTable(
                name: "user_plan_entitlements");

            migrationBuilder.DropTable(
                name: "webhook_events");

            migrationBuilder.DropTable(
                name: "payment_orders");

            migrationBuilder.DropIndex(
                name: "IX_card_unlocks_payment_order_id",
                table: "card_unlocks");

            migrationBuilder.DropIndex(
                name: "IX_card_unlocks_user_plan_entitlement_id",
                table: "card_unlocks");

            migrationBuilder.DropColumn(
                name: "payment_order_id",
                table: "card_unlocks");

            migrationBuilder.DropColumn(
                name: "user_plan_entitlement_id",
                table: "card_unlocks");
        }
    }
}
