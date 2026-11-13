using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameSavedCardCheckIdToFunCardId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_cards_checks_check_id",
                table: "saved_cards");

            migrationBuilder.DropIndex(
                name: "IX_saved_cards_check_id",
                table: "saved_cards");

            migrationBuilder.DropIndex(
                name: "IX_saved_cards_user_check",
                table: "saved_cards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_saved_cards_card_type_matches_target",
                table: "saved_cards");

            migrationBuilder.RenameColumn(
                name: "check_id",
                table: "saved_cards",
                newName: "funcard_id");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_funcard_id",
                table: "saved_cards",
                column: "funcard_id",
                filter: "\"funcard_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_funcard",
                table: "saved_cards",
                columns: new[] { "user_id", "funcard_id" },
                unique: true,
                filter: "\"funcard_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_saved_cards_card_type_matches_target",
                table: "saved_cards",
                sql: "(\"card_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"funcard_id\" IS NULL) OR (\"card_type\" = 'FUNCARD' AND \"funcard_id\" IS NOT NULL AND \"poll_id\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_saved_cards_checks_funcard_id",
                table: "saved_cards",
                column: "funcard_id",
                principalTable: "checks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_cards_checks_funcard_id",
                table: "saved_cards");

            migrationBuilder.DropIndex(
                name: "IX_saved_cards_funcard_id",
                table: "saved_cards");

            migrationBuilder.DropIndex(
                name: "IX_saved_cards_user_funcard",
                table: "saved_cards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_saved_cards_card_type_matches_target",
                table: "saved_cards");

            migrationBuilder.RenameColumn(
                name: "funcard_id",
                table: "saved_cards",
                newName: "check_id");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_check_id",
                table: "saved_cards",
                column: "check_id",
                filter: "\"check_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_check",
                table: "saved_cards",
                columns: new[] { "user_id", "check_id" },
                unique: true,
                filter: "\"check_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_saved_cards_card_type_matches_target",
                table: "saved_cards",
                sql: "(\"card_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"check_id\" IS NULL) OR (\"card_type\" = 'FUNCARD' AND \"check_id\" IS NOT NULL AND \"poll_id\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_saved_cards_checks_check_id",
                table: "saved_cards",
                column: "check_id",
                principalTable: "checks",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
