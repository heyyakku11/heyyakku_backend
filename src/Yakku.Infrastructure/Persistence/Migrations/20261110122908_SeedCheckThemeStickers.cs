using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedCheckThemeStickers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sample launch themes with stickers. Inserts any that are missing and fills the sticker
            // only where none was set, so themes an admin already customised are left alone.
            migrationBuilder.Sql("""
                INSERT INTO check_types (id, name, background_color, label_color, sticker) VALUES
                    (gen_random_uuid(), 'Mirror Check', '#FFE0E8', '#993556', 'be honest'),
                    (gen_random_uuid(), 'Vibe Check',   '#ECE3FF', '#4A3A8C', 'no filter'),
                    (gen_random_uuid(), 'Trust Check',  '#DDF3E4', '#245C3A', 'real talk')
                ON CONFLICT (name) DO UPDATE
                    SET sticker = EXCLUDED.sticker
                    WHERE check_types.sticker IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE check_types SET sticker = NULL
                WHERE (name, sticker) IN (('Mirror Check', 'be honest'), ('Vibe Check', 'no filter'), ('Trust Check', 'real talk'));
                """);
        }
    }
}
