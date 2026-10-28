using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReasonReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reason_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reason_reports", x => x.id);
                    table.CheckConstraint("CK_reason_reports_content_type", "\"content_type\" IN ('POLL', 'CHECK')");
                    table.ForeignKey(
                        name: "FK_reason_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_content",
                table: "reason_reports",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_created_at",
                table: "reason_reports",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_reporter_id",
                table: "reason_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_reason_reports_vote_id",
                table: "reason_reports",
                column: "vote_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reason_reports");
        }
    }
}
