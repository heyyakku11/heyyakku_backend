using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrimNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertsEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "NormalEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "SmsEnabled",
                table: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "WhatsAppEnabled",
                table: "NotificationPreferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AlertsEnabled",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NormalEnabled",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);

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
        }
    }
}
