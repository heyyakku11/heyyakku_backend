using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedFunCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_fun_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_fun_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_fun_cards_checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_fun_cards_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedFunCards_CheckId",
                table: "saved_fun_cards",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFunCards_UserId_CheckId",
                table: "saved_fun_cards",
                columns: new[] { "UserId", "CheckId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_fun_cards");
        }
    }
}
