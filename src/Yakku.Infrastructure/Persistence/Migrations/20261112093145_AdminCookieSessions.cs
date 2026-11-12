using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminCookieSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deliberate, one-time session invalidation. Old rows are refresh-token sessions with no equivalent in the
            // cookie-session model (and the new unique hash index could not hold their placeholder values), so every
            // admin signs in again once after this deploy. Admin accounts are not touched.
            migrationBuilder.Sql("DELETE FROM admin_sessions;");

            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_AdminId",
                table: "admin_sessions");

            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_RefreshTokenHash",
                table: "admin_sessions");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                table: "admin_sessions");

            migrationBuilder.RenameColumn(
                name: "LastUsedAt",
                table: "admin_sessions",
                newName: "PreviousHashValidUntil");

            migrationBuilder.RenameColumn(
                name: "ExpiresAt",
                table: "admin_sessions",
                newName: "RotatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "admin_sessions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AbsoluteExpiresAt",
                table: "admin_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "IdleExpiresAt",
                table: "admin_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "admin_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PreviousSessionHash",
                table: "admin_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                table: "admin_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionHash",
                table: "admin_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AbsoluteExpiresAt",
                table: "admin_sessions",
                column: "AbsoluteExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminId_RevokedAt",
                table: "admin_sessions",
                columns: new[] { "AdminId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_PreviousSessionHash",
                table: "admin_sessions",
                column: "PreviousSessionHash");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_SessionHash",
                table: "admin_sessions",
                column: "SessionHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM admin_sessions;");

            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_AbsoluteExpiresAt",
                table: "admin_sessions");

            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_AdminId_RevokedAt",
                table: "admin_sessions");

            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_PreviousSessionHash",
                table: "admin_sessions");

            migrationBuilder.DropIndex(
                name: "IX_AdminSessions_SessionHash",
                table: "admin_sessions");

            migrationBuilder.DropColumn(
                name: "AbsoluteExpiresAt",
                table: "admin_sessions");

            migrationBuilder.DropColumn(
                name: "IdleExpiresAt",
                table: "admin_sessions");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "admin_sessions");

            migrationBuilder.DropColumn(
                name: "PreviousSessionHash",
                table: "admin_sessions");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "admin_sessions");

            migrationBuilder.DropColumn(
                name: "SessionHash",
                table: "admin_sessions");

            migrationBuilder.RenameColumn(
                name: "RotatedAt",
                table: "admin_sessions",
                newName: "ExpiresAt");

            migrationBuilder.RenameColumn(
                name: "PreviousHashValidUntil",
                table: "admin_sessions",
                newName: "LastUsedAt");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "admin_sessions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                table: "admin_sessions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminId",
                table: "admin_sessions",
                column: "AdminId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_RefreshTokenHash",
                table: "admin_sessions",
                column: "RefreshTokenHash",
                unique: true);
        }
    }
}
