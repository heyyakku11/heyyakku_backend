using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPollShareCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "share_code",
                table: "polls",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            // Give every existing poll its own code before the unique index goes on.
            migrationBuilder.Sql(
                "UPDATE polls SET share_code = substr(md5(id::text || random()::text || clock_timestamp()::text), 1, 12);");

            migrationBuilder.CreateIndex(
                name: "IX_polls_share_code",
                table: "polls",
                column: "share_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_polls_share_code",
                table: "polls");

            migrationBuilder.DropColumn(
                name: "share_code",
                table: "polls");
        }
    }
}
