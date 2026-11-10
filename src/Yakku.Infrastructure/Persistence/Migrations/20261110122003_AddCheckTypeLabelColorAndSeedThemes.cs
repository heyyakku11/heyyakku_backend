using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckTypeLabelColorAndSeedThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "label_color",
                table: "check_types",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#21191D"); // a valid color for any theme row that already exists

            migrationBuilder.AddCheckConstraint(
                name: "CK_check_types_label_color",
                table: "check_types",
                sql: "\"label_color\" ~ '^#[0-9A-Fa-f]{6}$'");

            // The three launch themes: name, background color and label color. Idempotent (skips names that exist).
            migrationBuilder.Sql("""
                INSERT INTO check_types (id, name, background_color, label_color, sticker) VALUES
                    (gen_random_uuid(), 'Mirror Check', '#FFE0E8', '#993556', NULL),
                    (gen_random_uuid(), 'Vibe Check',   '#ECE3FF', '#4A3A8C', NULL),
                    (gen_random_uuid(), 'Trust Check',  '#DDF3E4', '#245C3A', NULL)
                ON CONFLICT (name) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the seeded launch themes that no check uses are removed.
            migrationBuilder.Sql("""
                DELETE FROM check_types t
                WHERE t.name IN ('Mirror Check', 'Vibe Check', 'Trust Check')
                  AND NOT EXISTS (SELECT 1 FROM checks c WHERE c.check_type_id = t.id);
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_check_types_label_color",
                table: "check_types");

            migrationBuilder.DropColumn(
                name: "label_color",
                table: "check_types");
        }
    }
}
