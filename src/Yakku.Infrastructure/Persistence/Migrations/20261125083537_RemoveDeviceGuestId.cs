using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeviceGuestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM devices WHERE "GuestId" IS NOT NULL AND "UserId" IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_devices_guests_GuestId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_GuestId",
                table: "devices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_devices_single_owner",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "devices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GuestId",
                table: "devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_GuestId",
                table: "devices",
                column: "GuestId",
                filter: "\"GuestId\" IS NOT NULL");

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
    }
}
