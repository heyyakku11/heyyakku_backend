using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReasonReportModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reporter_reason",
                table: "reason_reports",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "reason_reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "reason_reports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "APPROVED");

            // Reports logged before moderation already removed their reason, so they count as approved, and no
            // reporter reason was recorded for them. New rows always set both themselves, so the defaults used to
            // backfill are dropped.
            migrationBuilder.Sql("UPDATE reason_reports SET reviewed_at = created_at;");
            migrationBuilder.Sql("ALTER TABLE reason_reports ALTER COLUMN reporter_reason DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE reason_reports ALTER COLUMN status DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_status_created_at",
                table: "reason_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_reason_reports_status",
                table: "reason_reports",
                sql: "\"status\" IN ('PENDING', 'APPROVED', 'REJECTED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reason_reports_status_created_at",
                table: "reason_reports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_reason_reports_status",
                table: "reason_reports");

            migrationBuilder.DropColumn(
                name: "reporter_reason",
                table: "reason_reports");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "reason_reports");

            migrationBuilder.DropColumn(
                name: "status",
                table: "reason_reports");
        }
    }
}
