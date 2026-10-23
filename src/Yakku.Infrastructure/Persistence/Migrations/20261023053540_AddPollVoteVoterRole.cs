using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPollVoteVoterRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_poll_votes_users_voter_id",
                table: "poll_votes");

            migrationBuilder.AddColumn<string>(
                name: "voter_role",
                table: "poll_votes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "USER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Guest voter ids are not users, so they cannot satisfy the restored foreign key.
            migrationBuilder.Sql("DELETE FROM poll_votes WHERE voter_role = 'GUEST';");

            migrationBuilder.DropColumn(
                name: "voter_role",
                table: "poll_votes");

            migrationBuilder.AddForeignKey(
                name: "FK_poll_votes_users_voter_id",
                table: "poll_votes",
                column: "voter_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
