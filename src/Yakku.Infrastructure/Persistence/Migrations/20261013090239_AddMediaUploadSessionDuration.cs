using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUploadSessionDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SmsEnabled",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppEnabled",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "duration_ms",
                table: "media_upload_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_media_upload_sessions_duration_non_negative",
                table: "media_upload_sessions",
                sql: "\"duration_ms\" IS NULL OR \"duration_ms\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_media_upload_sessions_duration_non_negative",
                table: "media_upload_sessions");

            migrationBuilder.DropColumn(
                name: "SmsEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "media_upload_sessions");
        }
    }
}
