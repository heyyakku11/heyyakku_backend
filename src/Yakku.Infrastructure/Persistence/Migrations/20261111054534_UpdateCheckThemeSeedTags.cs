using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCheckThemeSeedTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The three launch themes with their final colors and tag (stored as the sticker).
            // Inserts missing ones and overwrites the seeded values of existing ones.
            migrationBuilder.Sql("""
                INSERT INTO check_types (id, name, background_color, label_color, sticker) VALUES
                    (gen_random_uuid(), 'Mirror Check', '#FFE0E8', '#993556', 'how people see you'),
                    (gen_random_uuid(), 'Vibe Check',   '#ECE3FF', '#4A3A8C', 'energy and personality'),
                    (gen_random_uuid(), 'Trust Check',  '#DDF3E4', '#245C3A', 'reliability, secrets')
                ON CONFLICT (name) DO UPDATE
                    SET background_color = EXCLUDED.background_color,
                        label_color = EXCLUDED.label_color,
                        sticker = EXCLUDED.sticker;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the previous seed (colors unchanged, the earlier sample stickers).
            migrationBuilder.Sql("""
                UPDATE check_types SET sticker = CASE name
                    WHEN 'Mirror Check' THEN 'be honest'
                    WHEN 'Vibe Check' THEN 'no filter'
                    WHEN 'Trust Check' THEN 'real talk' END
                WHERE name IN ('Mirror Check', 'Vibe Check', 'Trust Check');
                """);
        }
    }
}
