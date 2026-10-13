using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OneAdminSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "AdminSessions" AS older
                USING "AdminSessions" AS newer
                WHERE older."AdminId" = newer."AdminId"
                  AND (
                      older."CreatedAt" < newer."CreatedAt"
                      OR (older."CreatedAt" = newer."CreatedAt" AND older."Id" < newer."Id")
                  );
                """);

            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_AdminId",
                table: "AdminSessions");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminId",
                table: "AdminSessions",
                column: "AdminId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_AdminId",
                table: "AdminSessions");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminId",
                table: "AdminSessions",
                column: "AdminId");
        }
    }
}
