using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowGuestPersonalityVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_personality_votes_card_id_voter_user_id",
                table: "personality_votes");

            migrationBuilder.AlterColumn<Guid>(
                name: "voter_user_id",
                table: "personality_votes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "voter_guest_id",
                table: "personality_votes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_card_id_voter_guest_id",
                table: "personality_votes",
                columns: new[] { "personality_card_id", "voter_guest_id" },
                unique: true,
                filter: "\"voter_guest_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_card_id_voter_user_id",
                table: "personality_votes",
                columns: new[] { "personality_card_id", "voter_user_id" },
                unique: true,
                filter: "\"voter_user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_voter_guest_id",
                table: "personality_votes",
                column: "voter_guest_id",
                filter: "\"voter_guest_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_personality_votes_one_voter",
                table: "personality_votes",
                sql: "(\"voter_user_id\" IS NOT NULL AND \"voter_guest_id\" IS NULL) OR (\"voter_user_id\" IS NULL AND \"voter_guest_id\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_personality_votes_guests_voter_guest_id",
                table: "personality_votes",
                column: "voter_guest_id",
                principalTable: "guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_personality_votes_guests_voter_guest_id",
                table: "personality_votes");

            migrationBuilder.DropIndex(
                name: "IX_personality_votes_card_id_voter_guest_id",
                table: "personality_votes");

            migrationBuilder.DropIndex(
                name: "IX_personality_votes_card_id_voter_user_id",
                table: "personality_votes");

            migrationBuilder.DropIndex(
                name: "IX_personality_votes_voter_guest_id",
                table: "personality_votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_personality_votes_one_voter",
                table: "personality_votes");

            migrationBuilder.DropColumn(
                name: "voter_guest_id",
                table: "personality_votes");

            migrationBuilder.AlterColumn<Guid>(
                name: "voter_user_id",
                table: "personality_votes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_card_id_voter_user_id",
                table: "personality_votes",
                columns: new[] { "personality_card_id", "voter_user_id" },
                unique: true);
        }
    }
}
