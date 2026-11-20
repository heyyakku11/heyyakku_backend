using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AskBackAskerQuotaOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ask_back_conversations_message_counts",
                table: "ask_back_conversations");

            migrationBuilder.AddColumn<int>(
                name: "applied_message_limit",
                table: "ask_back_conversations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "asker_quota_offset",
                table: "ask_back_conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ask_back_conversations_message_counts",
                table: "ask_back_conversations",
                sql: "\"asker_message_count\" >= 0 AND \"voter_message_count\" >= 0 AND \"asker_quota_offset\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ask_back_conversations_message_counts",
                table: "ask_back_conversations");

            migrationBuilder.DropColumn(
                name: "applied_message_limit",
                table: "ask_back_conversations");

            migrationBuilder.DropColumn(
                name: "asker_quota_offset",
                table: "ask_back_conversations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ask_back_conversations_message_counts",
                table: "ask_back_conversations",
                sql: "\"asker_message_count\" >= 0 AND \"voter_message_count\" >= 0");
        }
    }
}
