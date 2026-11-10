using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckTypeThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "check_type_id",
                table: "checks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "check_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    background_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    sticker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_types", x => x.id);
                    table.CheckConstraint("CK_check_types_background_color", "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_check_types_name_not_blank", "char_length(btrim(\"name\")) > 0");
                    table.CheckConstraint("CK_check_types_sticker_not_blank", "\"sticker\" IS NULL OR char_length(btrim(\"sticker\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_checks_check_type_id",
                table: "checks",
                column: "check_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_check_types_name",
                table: "check_types",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_checks_check_types_check_type_id",
                table: "checks",
                column: "check_type_id",
                principalTable: "check_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Remove the four checks that AddChecks seeded. Checks are now created by admins from admin-defined themes.
            // Only untouched rows (exact seeded title and question) that nobody has used are removed; their options cascade.
            // A seeded check that users already made is kept, because those users' cards and votes point at it.
            migrationBuilder.Sql("""
                DELETE FROM checks c
                WHERE (c.title, c.question) IN (
                    ('Mirror Check',  'Do people like me, or just like having me around?'),
                    ('Trust Check',   'If I told you a secret, how long before someone else knows?'),
                    ('Vibe Check',    'When I walk in, does the energy go up?'),
                    ('Loyalty Check', 'Would you still pick me if it got awkward?'))
                  AND NOT EXISTS (SELECT 1 FROM user_checks uc WHERE uc.check_id = c.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_checks_check_types_check_type_id",
                table: "checks");

            migrationBuilder.DropTable(
                name: "check_types");

            migrationBuilder.DropIndex(
                name: "IX_checks_check_type_id",
                table: "checks");

            migrationBuilder.DropColumn(
                name: "check_type_id",
                table: "checks");
        }
    }
}
