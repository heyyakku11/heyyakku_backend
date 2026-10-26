using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakePushAllowedPerDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPushAllowed",
                table: "devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The old switch was per user and says nothing about a given phone, so it is not copied to every device.
            // A device starts off. It is switched on only if the device itself last reported a granted OS permission
            // AND its user had not switched push off. Everything else (unknown or denied permission, a user who
            // opted out, guest or unowned devices) stays off until the app reports a granted permission again.
            migrationBuilder.Sql("""
                UPDATE devices d
                SET "IsPushAllowed" = TRUE
                FROM user_profiles p
                WHERE p."UserId" = d."UserId"
                  AND p."IsPushAllowed"
                  AND d."NotificationPermission" = 'Granted';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_devices_push_allowed_requires_granted",
                table: "devices",
                sql: "NOT \"IsPushAllowed\" OR COALESCE(\"NotificationPermission\" = 'Granted', FALSE)");

            migrationBuilder.DropColumn(
                name: "IsPushAllowed",
                table: "user_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPushAllowed",
                table: "user_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Best effort: a user is opted out when they have devices with a granted permission and none is on.
            migrationBuilder.Sql("""
                UPDATE user_profiles p
                SET "IsPushAllowed" = FALSE
                WHERE EXISTS (
                        SELECT 1 FROM devices d
                        WHERE d."UserId" = p."UserId" AND d."NotificationPermission" = 'Granted')
                  AND NOT EXISTS (
                        SELECT 1 FROM devices d
                        WHERE d."UserId" = p."UserId" AND d."IsPushAllowed");
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_devices_push_allowed_requires_granted",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "IsPushAllowed",
                table: "devices");
        }
    }
}
