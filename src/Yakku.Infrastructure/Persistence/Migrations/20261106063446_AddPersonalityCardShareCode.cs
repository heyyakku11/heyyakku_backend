using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalityCardShareCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_share_codes_content_type",
                table: "share_codes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_share_codes_one_target",
                table: "share_codes");

            migrationBuilder.AddColumn<Guid>(
                name: "personality_card_id",
                table: "share_codes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "share_code",
                table: "personality_cards",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_share_codes_personality_card_id",
                table: "share_codes",
                column: "personality_card_id",
                unique: true,
                filter: "\"personality_card_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_share_codes_content_type",
                table: "share_codes",
                sql: "\"content_type\" IN ('POLL', 'CHECK', 'PERSONALITY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_share_codes_one_target",
                table: "share_codes",
                sql: "(\"content_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"user_check_id\" IS NULL AND \"personality_card_id\" IS NULL)\nOR\n(\"content_type\" = 'CHECK' AND \"poll_id\" IS NULL AND \"user_check_id\" IS NOT NULL AND \"personality_card_id\" IS NULL)\nOR\n(\"content_type\" = 'PERSONALITY' AND \"poll_id\" IS NULL AND \"user_check_id\" IS NULL AND \"personality_card_id\" IS NOT NULL)");

            // Give every existing card a code and register it. Random 6-hex codes can collide, so each card retries
            // until its registry insert succeeds (the registry key is unique across polls, checks and cards). Only
            // cards without a code are touched, so running this again changes nothing.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    card record;
                    candidate text;
                BEGIN
                    FOR card IN SELECT id FROM personality_cards WHERE share_code IS NULL LOOP
                        LOOP
                            candidate := substr(md5(random()::text || clock_timestamp()::text || card.id::text), 1, 6);
                            BEGIN
                                INSERT INTO share_codes (code, content_type, personality_card_id, created_at)
                                VALUES (candidate, 'PERSONALITY', card.id, now());
                                UPDATE personality_cards SET share_code = candidate WHERE id = card.id;
                                EXIT;
                            EXCEPTION WHEN unique_violation THEN
                                NULL;
                            END;
                        END LOOP;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "share_code",
                table: "personality_cards",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_cards_share_code",
                table: "personality_cards",
                column: "share_code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_share_codes_personality_cards_personality_card_id",
                table: "share_codes",
                column: "personality_card_id",
                principalTable: "personality_cards",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_share_codes_personality_cards_personality_card_id",
                table: "share_codes");

            migrationBuilder.DropIndex(
                name: "IX_share_codes_personality_card_id",
                table: "share_codes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_share_codes_content_type",
                table: "share_codes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_share_codes_one_target",
                table: "share_codes");

            migrationBuilder.Sql("DELETE FROM share_codes WHERE content_type = 'PERSONALITY';");

            migrationBuilder.DropIndex(
                name: "IX_personality_cards_share_code",
                table: "personality_cards");

            migrationBuilder.DropColumn(
                name: "personality_card_id",
                table: "share_codes");

            migrationBuilder.DropColumn(
                name: "share_code",
                table: "personality_cards");

            migrationBuilder.AddCheckConstraint(
                name: "CK_share_codes_content_type",
                table: "share_codes",
                sql: "\"content_type\" IN ('POLL', 'CHECK')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_share_codes_one_target",
                table: "share_codes",
                sql: "(\"content_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"user_check_id\" IS NULL)\nOR\n(\"content_type\" = 'CHECK' AND \"poll_id\" IS NULL AND \"user_check_id\" IS NOT NULL)");
        }
    }
}
