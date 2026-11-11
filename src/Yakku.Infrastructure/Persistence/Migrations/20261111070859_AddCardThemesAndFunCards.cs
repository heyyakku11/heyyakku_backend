using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Replaces check_types with card_themes, the one table for the themes of every kind of card, and adds
    /// "just for fun" cards: checks of kind FUN that have no live week. Existing check themes keep their ids.
    /// </summary>
    public partial class AddCardThemesAndFunCards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "card_themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    background_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    label_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_card_themes", x => x.id);
                    table.CheckConstraint("CK_card_themes_background_color", "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_card_themes_label_color", "\"label_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_card_themes_name_not_blank", "char_length(btrim(\"name\")) > 0");
                    table.CheckConstraint("CK_card_themes_tag_not_blank", "\"tag\" IS NULL OR char_length(btrim(\"tag\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_card_themes_kind_name",
                table: "card_themes",
                columns: new[] { "kind", "name" },
                unique: true);

            // Move every existing check theme across, keeping its id so checks stay linked. The old sticker
            // column held the theme's tag line.
            migrationBuilder.Sql("""
                INSERT INTO card_themes (id, kind, name, background_color, label_color, tag)
                SELECT id, 'CHECK', name, background_color, label_color, sticker FROM check_types;
                """);

            // Checks now point at card_themes (same ids), under a name that fits every card kind.
            migrationBuilder.DropForeignKey(
                name: "FK_checks_check_types_check_type_id",
                table: "checks");

            migrationBuilder.RenameColumn(
                name: "check_type_id",
                table: "checks",
                newName: "card_theme_id");

            migrationBuilder.RenameIndex(
                name: "IX_checks_check_type_id",
                table: "checks",
                newName: "IX_checks_card_theme_id");

            migrationBuilder.AddForeignKey(
                name: "FK_checks_card_themes_card_theme_id",
                table: "checks",
                column: "card_theme_id",
                principalTable: "card_themes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "check_types");

            // Checks gain a kind; just-for-fun cards have no live week.
            migrationBuilder.DropCheckConstraint(
                name: "CK_checks_live_week_start_monday",
                table: "checks");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "checks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WEEKLY");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "live_week_start",
                table: "checks",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddCheckConstraint(
                name: "CK_checks_kind_live_week_start",
                table: "checks",
                sql: "(\"kind\" = 'WEEKLY' AND \"live_week_start\" IS NOT NULL AND EXTRACT(ISODOW FROM \"live_week_start\") = 1) OR (\"kind\" = 'FUN' AND \"live_week_start\" IS NULL)");

            // The just-for-fun theme and a few sample cards for an admin to edit. Idempotent.
            migrationBuilder.Sql("""
                INSERT INTO card_themes (id, kind, name, background_color, label_color, tag)
                VALUES (gen_random_uuid(), 'FUN', 'Just for fun', '#FFEFC2', '#6B4B00', 'movies, jail, silly ones')
                ON CONFLICT (kind, name) DO NOTHING;

                DO $$
                DECLARE
                    theme_id uuid;
                    card_id uuid;
                    sample record;
                    option_text text;
                    pos int;
                BEGIN
                    SELECT id INTO theme_id FROM card_themes WHERE kind = 'FUN' AND name = 'Just for fun';

                    FOR sample IN
                        SELECT * FROM (VALUES
                            ('Which movie could you watch on repeat forever?',
                                ARRAY['A feel-good classic', 'A big action blockbuster', 'A cozy animated one']),
                            ('You wake up in jail. What did you do?',
                                ARRAY['Argued with a parking meter', 'Stole the last slice of pizza', 'Sang way too loud in public']),
                            ('Which silly superpower would you pick?',
                                ARRAY['Talking to pigeons', 'Always finding the perfect snack', 'Instantly knowing every song lyric']),
                            ('What would the movie about your life be called?',
                                ARRAY['Chaos but Make It Cute', 'The Snack Whisperer', 'Almost On Time']),
                            ('Who would you break out of jail with?',
                                ARRAY['My best friend', 'The funniest person I know', 'The one with the plan'])
                        ) AS v(question, options)
                    LOOP
                        IF EXISTS (SELECT 1 FROM checks WHERE kind = 'FUN' AND question = sample.question) THEN
                            CONTINUE;
                        END IF;

                        card_id := gen_random_uuid();
                        INSERT INTO checks (id, kind, live_week_start, title, question, background_color, sticker, is_active, card_theme_id)
                        VALUES (card_id, 'FUN', NULL, 'Just for fun', sample.question, '#FFEFC2', '', true, theme_id);

                        pos := 0;
                        FOREACH option_text IN ARRAY sample.options LOOP
                            pos := pos + 1;
                            INSERT INTO check_options (id, check_id, text, sort_order)
                            VALUES (gen_random_uuid(), card_id, option_text, pos);
                        END LOOP;
                    END LOOP;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Just-for-fun cards cannot exist without kind/null week, so they go (with anything made from them).
            migrationBuilder.Sql("""
                DELETE FROM user_checks WHERE check_id IN (SELECT id FROM checks WHERE kind = 'FUN');
                DELETE FROM checks WHERE kind = 'FUN';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_checks_kind_live_week_start",
                table: "checks");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "live_week_start",
                table: "checks",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "kind",
                table: "checks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_checks_live_week_start_monday",
                table: "checks",
                sql: "EXTRACT(ISODOW FROM \"live_week_start\") = 1");

            migrationBuilder.CreateTable(
                name: "check_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    background_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    label_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sticker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_types", x => x.id);
                    table.CheckConstraint("CK_check_types_background_color", "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_check_types_label_color", "\"label_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("CK_check_types_name_not_blank", "char_length(btrim(\"name\")) > 0");
                    table.CheckConstraint("CK_check_types_sticker_not_blank", "\"sticker\" IS NULL OR char_length(btrim(\"sticker\")) > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_check_types_name",
                table: "check_types",
                column: "name",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO check_types (id, name, background_color, label_color, sticker)
                SELECT id, name, background_color, label_color, tag FROM card_themes WHERE kind = 'CHECK';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_checks_card_themes_card_theme_id",
                table: "checks");

            migrationBuilder.RenameColumn(
                name: "card_theme_id",
                table: "checks",
                newName: "check_type_id");

            migrationBuilder.RenameIndex(
                name: "IX_checks_card_theme_id",
                table: "checks",
                newName: "IX_checks_check_type_id");

            migrationBuilder.AddForeignKey(
                name: "FK_checks_check_types_check_type_id",
                table: "checks",
                column: "check_type_id",
                principalTable: "check_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "card_themes");
        }
    }
}
