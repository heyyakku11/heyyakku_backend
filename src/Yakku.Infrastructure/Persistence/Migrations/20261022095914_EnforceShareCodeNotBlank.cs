using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceShareCodeNotBlank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_user_checks_share_code_not_blank",
                table: "user_checks",
                sql: "char_length(btrim(\"share_code\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_polls_share_code_not_blank",
                table: "polls",
                sql: "char_length(btrim(\"share_code\")) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_user_checks_share_code_not_blank",
                table: "user_checks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_polls_share_code_not_blank",
                table: "polls");
        }
    }
}
