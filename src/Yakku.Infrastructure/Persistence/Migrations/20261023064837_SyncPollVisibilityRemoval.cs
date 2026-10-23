using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncPollVisibilityRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dropping polls.visibility already removes the check constraint and the index that referenced it,
            // so these are no-ops wherever RemovePollVisibility has run. They only bring the model in line.
            migrationBuilder.Sql("ALTER TABLE polls DROP CONSTRAINT IF EXISTS \"CK_polls_visibility\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_polls_active_public_created_at\";");

            migrationBuilder.CreateIndex(
                name: "IX_polls_active_created_at",
                table: "polls",
                column: "created_at",
                descending: new bool[0],
                filter: "\"status\" = 'ACTIVE'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_polls_active_created_at",
                table: "polls");
        }
    }
}
