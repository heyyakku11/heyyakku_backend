using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPollClosedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "closed_at",
                table: "polls",
                type: "timestamp with time zone",
                nullable: true);

            // Polls closed before this column existed: the close time was kept in updated_at.
            migrationBuilder.Sql("UPDATE polls SET closed_at = updated_at WHERE status = 'CLOSED';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "closed_at",
                table: "polls");
        }
    }
}
