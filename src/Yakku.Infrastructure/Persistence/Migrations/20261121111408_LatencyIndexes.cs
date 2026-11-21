using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LatencyIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_user_checks_user_id_created_at",
                table: "user_checks",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_voter_id_created_at",
                table: "poll_votes",
                columns: new[] { "voter_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_checks_user_id_created_at",
                table: "user_checks");

            migrationBuilder.DropIndex(
                name: "IX_poll_votes_voter_id_created_at",
                table: "poll_votes");
        }
    }
}
