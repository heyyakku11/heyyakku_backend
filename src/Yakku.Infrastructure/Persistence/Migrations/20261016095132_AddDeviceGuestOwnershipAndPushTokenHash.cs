using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceGuestOwnershipAndPushTokenHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GuestId",
                table: "devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushTokenHash",
                table: "devices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Backfill the hash for existing tokens and make them unique before the unique index and the
            // token/hash consistency check are created. If several devices share a token (stale rows from
            // reinstalls), only the most recently updated one keeps it; the rest just lose push delivery.
            migrationBuilder.Sql("""
                UPDATE devices SET "PushToken" = NULL WHERE "PushToken" IS NOT NULL AND btrim("PushToken") = '';
                UPDATE devices
                   SET "PushTokenHash" = encode(sha256(convert_to("PushToken", 'UTF8')), 'hex')
                 WHERE "PushToken" IS NOT NULL;
                UPDATE devices d
                   SET "PushToken" = NULL, "PushTokenHash" = NULL
                  FROM (SELECT "Id", row_number() OVER (PARTITION BY "PushTokenHash" ORDER BY "UpdatedAt" DESC, "Id") AS rn
                          FROM devices WHERE "PushTokenHash" IS NOT NULL) ranked
                 WHERE d."Id" = ranked."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_GuestId",
                table: "devices",
                column: "GuestId",
                filter: "\"GuestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_PushTokenHash",
                table: "devices",
                column: "PushTokenHash",
                unique: true,
                filter: "\"PushTokenHash\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_devices_push_token_hash_matches_token",
                table: "devices",
                sql: "(\"PushToken\" IS NULL) = (\"PushTokenHash\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_devices_single_owner",
                table: "devices",
                sql: "NOT (\"UserId\" IS NOT NULL AND \"GuestId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_guests_GuestId",
                table: "devices",
                column: "GuestId",
                principalTable: "guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_guests_GuestId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_GuestId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_PushTokenHash",
                table: "devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_devices_push_token_hash_matches_token",
                table: "devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_devices_single_owner",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "PushTokenHash",
                table: "devices");
        }
    }
}
