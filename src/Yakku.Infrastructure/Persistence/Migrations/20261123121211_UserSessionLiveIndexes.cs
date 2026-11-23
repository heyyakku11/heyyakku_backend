using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserSessionLiveIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_Id_UserId_Live",
                table: "user_sessions",
                columns: new[] { "Id", "UserId" },
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_Live",
                table: "user_sessions",
                columns: new[] { "UserId", "ExpiresAt" },
                filter: "\"RevokedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessions_Id_UserId_Live",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserId_Live",
                table: "user_sessions");
        }
    }
}
