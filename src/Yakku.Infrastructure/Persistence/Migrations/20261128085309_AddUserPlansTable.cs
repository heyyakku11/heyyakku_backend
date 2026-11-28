using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves a user's plan out of the users table into user_plans (1:1 over user_id): plan_code, plan_activated_at and
    /// plan_expires_at. Every user gets a row, copied from their current plan, before the old columns are dropped.
    /// </summary>
    /// <inheritdoc />
    public partial class AddUserPlansTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // user_plans.plan_code points at plans.code, so the code needs to be a key rather than just a unique index.
            migrationBuilder.DropIndex(
                name: "IX_plans_code",
                table: "plans");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_plans_code",
                table: "plans",
                column: "code");

            migrationBuilder.CreateTable(
                name: "user_plans",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "free"),
                    plan_activated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plan_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_plans", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_plans_plans_plan_code",
                        column: x => x.plan_code,
                        principalTable: "plans",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_plans_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_plans_plan_code",
                table: "user_plans",
                column: "plan_code");

            // One row per existing user, carrying the plan and its dates over from the users table.
            migrationBuilder.Sql("""
                INSERT INTO user_plans (user_id, plan_code, plan_activated_at, plan_expires_at)
                SELECT u."Id", p.code, u."PlanActivatedAt", u."PlanExpiresAt"
                FROM users u
                JOIN plans p ON p.id = u."PlanId";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_users_plans_PlanId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PlanId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PlanActivatedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            // Users without a row (there should be none) stay on the free plan through this default.
            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("a1f3c0de-0000-4000-8000-000000000001"));

            // Copy each user's plan back from user_plans before the table is dropped.
            migrationBuilder.Sql("""
                UPDATE users u
                SET "PlanId" = p.id,
                    "PlanActivatedAt" = up.plan_activated_at,
                    "PlanExpiresAt" = up.plan_expires_at
                FROM user_plans up
                JOIN plans p ON p.code = up.plan_code
                WHERE up.user_id = u."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PlanId",
                table: "users",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_plans_PlanId",
                table: "users",
                column: "PlanId",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "user_plans");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_plans_code",
                table: "plans");

            migrationBuilder.CreateIndex(
                name: "IX_plans_code",
                table: "plans",
                column: "code",
                unique: true);
        }
    }
}
