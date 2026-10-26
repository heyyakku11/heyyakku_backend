using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeviceCurrentSessionAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_user_sessions_DeviceId",
                table: "user_sessions",
                newName: "IX_UserSessions_DeviceId");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentSessionId",
                table: "devices",
                type: "uuid",
                nullable: true);

            // Existing data: an installation's current session is its newest live session of the same user.
            // Installations with no such session stay unassociated with a session (and so receive no push) until
            // the next login or device registration binds them.
            migrationBuilder.Sql("""
                UPDATE devices d
                SET "CurrentSessionId" = s."Id"
                FROM (
                    SELECT DISTINCT ON ("DeviceId") "Id", "DeviceId", "UserId"
                    FROM user_sessions
                    WHERE "DeviceId" IS NOT NULL AND "RevokedAt" IS NULL AND "ExpiresAt" > now()
                    ORDER BY "DeviceId", "LastActiveAt" DESC NULLS LAST, "CreatedAt" DESC
                ) s
                WHERE d."Id" = s."DeviceId" AND d."UserId" = s."UserId";
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_user_sessions_Id_UserId",
                table: "user_sessions",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_CurrentSessionId",
                table: "devices",
                column: "CurrentSessionId",
                unique: true,
                filter: "\"CurrentSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_devices_CurrentSessionId_UserId",
                table: "devices",
                columns: new[] { "CurrentSessionId", "UserId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_devices_current_session_has_user",
                table: "devices",
                sql: "\"CurrentSessionId\" IS NULL OR \"UserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_user_sessions_CurrentSessionId_UserId",
                table: "devices",
                columns: new[] { "CurrentSessionId", "UserId" },
                principalTable: "user_sessions",
                principalColumns: new[] { "Id", "UserId" },
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_user_sessions_CurrentSessionId_UserId",
                table: "devices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_user_sessions_Id_UserId",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "IX_Devices_CurrentSessionId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_CurrentSessionId_UserId",
                table: "devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_devices_current_session_has_user",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "CurrentSessionId",
                table: "devices");

            migrationBuilder.RenameIndex(
                name: "IX_UserSessions_DeviceId",
                table: "user_sessions",
                newName: "IX_user_sessions_DeviceId");
        }
    }
}
