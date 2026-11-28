using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SetFreeAskBackLimitToZero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_plans_ask_back_message_limit",
                table: "plans");

            migrationBuilder.AddCheckConstraint(
                name: "CK_plans_ask_back_message_limit",
                table: "plans",
                sql: "\"ask_back_message_limit\" IS NULL OR \"ask_back_message_limit\" >= 0");

            migrationBuilder.Sql("""
                UPDATE plans
                SET ask_back_message_limit = 0, updated_at = now()
                WHERE code = 'free' AND ask_back_message_limit IS DISTINCT FROM 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE plans
                SET ask_back_message_limit = 1, updated_at = now()
                WHERE code = 'free' AND ask_back_message_limit IS DISTINCT FROM 1;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_plans_ask_back_message_limit",
                table: "plans");

            migrationBuilder.AddCheckConstraint(
                name: "CK_plans_ask_back_message_limit",
                table: "plans",
                sql: "\"ask_back_message_limit\" IS NULL OR \"ask_back_message_limit\" >= 1");
        }
    }
}
