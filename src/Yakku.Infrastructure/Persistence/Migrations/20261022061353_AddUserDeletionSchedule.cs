using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeletionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionRequestedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionScheduledFor",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletionScheduledFor",
                table: "users",
                column: "DeletionScheduledFor",
                filter: "\"DeletionScheduledFor\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_DeletionScheduledFor",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletionScheduledFor",
                table: "users");
        }
    }
}
