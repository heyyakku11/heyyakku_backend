using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUploadSessionGuestOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "media_upload_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "guest_id",
                table: "media_upload_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_guest_id_status",
                table: "media_upload_sessions",
                columns: new[] { "guest_id", "status" },
                filter: "\"guest_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_media_upload_sessions_owner_xor",
                table: "media_upload_sessions",
                sql: "(\"user_id\" IS NOT NULL AND \"guest_id\" IS NULL) OR (\"user_id\" IS NULL AND \"guest_id\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_media_upload_sessions_Guests_guest_id",
                table: "media_upload_sessions",
                column: "guest_id",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_media_upload_sessions_Guests_guest_id",
                table: "media_upload_sessions");

            migrationBuilder.DropIndex(
                name: "IX_media_upload_sessions_guest_id_status",
                table: "media_upload_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_media_upload_sessions_owner_xor",
                table: "media_upload_sessions");

            migrationBuilder.DropColumn(
                name: "guest_id",
                table: "media_upload_sessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "media_upload_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
