using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePollDeletedReasonWithTotalWhyCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedReason",
                table: "Polls");

            migrationBuilder.AddColumn<int>(
                name: "TotalWhyCount",
                table: "Polls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Polls" AS poll
                SET "TotalWhyCount" = counts.why_count
                FROM (
                    SELECT "PollId", COUNT(*)::int AS why_count
                    FROM "Votes"
                    WHERE "Reason" IS NOT NULL AND btrim("Reason") <> ''
                    GROUP BY "PollId"
                ) AS counts
                WHERE poll."Id" = counts."PollId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalWhyCount",
                table: "Polls");

            migrationBuilder.AddColumn<string>(
                name: "DeletedReason",
                table: "Polls",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
