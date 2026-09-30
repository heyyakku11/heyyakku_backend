using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAdminRoleWithEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Admins_AdminRoles_AdminRoleId",
                table: "Admins");

            migrationBuilder.DropIndex(
                name: "IX_Admins_AdminRoleId",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "AdminRoleId",
                table: "Admins");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Admins",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Admins" SET "Role" = 'SuperAdmin';""");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Admins",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "AdminRoles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Admins");

            migrationBuilder.AddColumn<Guid>(
                name: "AdminRoleId",
                table: "Admins",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AdminRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PermissionsList = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admins_AdminRoleId",
                table: "Admins",
                column: "AdminRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminRoles_Name",
                table: "AdminRoles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Admins_AdminRoles_AdminRoleId",
                table: "Admins",
                column: "AdminRoleId",
                principalTable: "AdminRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
