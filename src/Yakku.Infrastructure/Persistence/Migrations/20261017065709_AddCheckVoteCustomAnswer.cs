using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckVoteCustomAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "check_option_id",
                table: "user_check_votes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "custom_answer",
                table: "user_check_votes",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_check_votes_custom_answer_not_blank",
                table: "user_check_votes",
                sql: "\"custom_answer\" IS NULL OR char_length(btrim(\"custom_answer\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_user_check_votes_one_choice",
                table: "user_check_votes",
                sql: "(\"check_option_id\" IS NOT NULL AND \"custom_answer\" IS NULL)\nOR\n(\"check_option_id\" IS NULL AND \"custom_answer\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_check_votes_custom_answer_not_blank",
                table: "user_check_votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_user_check_votes_one_choice",
                table: "user_check_votes");

            migrationBuilder.DropColumn(
                name: "custom_answer",
                table: "user_check_votes");

            migrationBuilder.AlterColumn<Guid>(
                name: "check_option_id",
                table: "user_check_votes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
