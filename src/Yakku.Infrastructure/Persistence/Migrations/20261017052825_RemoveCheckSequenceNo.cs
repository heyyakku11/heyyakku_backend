using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCheckSequenceNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Checks in a week are now listed by creation time, so keep the existing order by spacing
            // created_at one second apart per sequence_no before the column goes away.
            migrationBuilder.Sql("""
                UPDATE checks
                SET created_at = created_at + (sequence_no * interval '1 second');
                """);

            migrationBuilder.DropIndex(
                name: "IX_checks_live_week_start_sequence_no",
                table: "checks");

            migrationBuilder.DropColumn(
                name: "sequence_no",
                table: "checks");

            migrationBuilder.CreateIndex(
                name: "IX_checks_live_week_start_created_at",
                table: "checks",
                columns: new[] { "live_week_start", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_checks_live_week_start_created_at",
                table: "checks");

            migrationBuilder.AddColumn<int>(
                name: "sequence_no",
                table: "checks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_checks_live_week_start_sequence_no",
                table: "checks",
                columns: new[] { "live_week_start", "sequence_no" },
                unique: true);
        }
    }
}
