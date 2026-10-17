using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("a1f3c0de-0000-4000-8000-000000000001"));

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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.id);
                    table.CheckConstraint("CK_plans_price_not_negative", "\"price_minor\" >= 0");
                });

            // The plans, in order, with prices in paise (INR). They must exist before the foreign key below;
            // existing users default to free.
            migrationBuilder.Sql("""
                INSERT INTO plans (id, code, name, sort_order, price_minor, currency) VALUES
                    ('a1f3c0de-0000-4000-8000-000000000001', 'free', 'Free', 1, 0, 'INR'),
                    ('a1f3c0de-0000-4000-8000-000000000002', 'premium', 'Premium', 2, 4900, 'INR'),
                    ('a1f3c0de-0000-4000-8000-000000000003', 'yakku_love', 'Yakku Love', 3, 12900, 'INR');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PlanId",
                table: "users",
                column: "PlanId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_users_plans_PlanId",
                table: "users",
                column: "PlanId",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_plans_PlanId",
                table: "users");

            migrationBuilder.DropTable(
                name: "plans");

            migrationBuilder.DropIndex(
                name: "IX_Users_PlanId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "users");
        }
    }
}
