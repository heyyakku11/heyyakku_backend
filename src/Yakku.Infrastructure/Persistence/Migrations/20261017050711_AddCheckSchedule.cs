using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "check_rotations");

            migrationBuilder.DropIndex(
                name: "IX_checks_sequence_no",
                table: "checks");

            migrationBuilder.AddColumn<DateOnly>(
                name: "live_week_start",
                table: "checks",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Checks seeded before scheduling existed go live in the week of this migration.
            migrationBuilder.Sql("UPDATE checks SET live_week_start = date_trunc('week', now())::date;");
            migrationBuilder.Sql("ALTER TABLE checks ALTER COLUMN live_week_start DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_checks_live_week_start_sequence_no",
                table: "checks",
                columns: new[] { "live_week_start", "sequence_no" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_checks_live_week_start_monday",
                table: "checks",
                sql: "EXTRACT(ISODOW FROM \"live_week_start\") = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_checks_live_week_start_sequence_no",
                table: "checks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_checks_live_week_start_monday",
                table: "checks");

            migrationBuilder.DropColumn(
                name: "live_week_start",
                table: "checks");

            migrationBuilder.CreateTable(
                name: "check_rotations",
                columns: table => new
                {
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_rotations", x => x.week_start);
                    table.ForeignKey(
                        name: "FK_check_rotations_checks_check_id",
                        column: x => x.check_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checks_sequence_no",
                table: "checks",
                column: "sequence_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_check_rotations_check_id",
                table: "check_rotations",
                column: "check_id");
        }
    }
}
