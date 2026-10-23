using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCheckVotesAndShareCodeRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_check_votes_users_voter_id",
                table: "user_check_votes");

            migrationBuilder.AddColumn<string>(
                name: "voter_role",
                table: "user_check_votes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "USER");

            migrationBuilder.CreateTable(
                name: "share_codes",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_check_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_codes", x => x.code);
                    table.CheckConstraint("CK_share_codes_code_not_blank", "char_length(btrim(\"code\")) > 0");
                    table.CheckConstraint("CK_share_codes_content_type", "\"content_type\" IN ('POLL', 'CHECK')");
                    table.CheckConstraint("CK_share_codes_one_target", "(\"content_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"user_check_id\" IS NULL)\nOR\n(\"content_type\" = 'CHECK' AND \"poll_id\" IS NULL AND \"user_check_id\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_share_codes_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_share_codes_user_checks_user_check_id",
                        column: x => x.user_check_id,
                        principalTable: "user_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_share_codes_poll_id",
                table: "share_codes",
                column: "poll_id",
                unique: true,
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_share_codes_user_check_id",
                table: "share_codes",
                column: "user_check_id",
                unique: true,
                filter: "\"user_check_id\" IS NOT NULL");

            // Existing rows keep their IDs and codes. Codes were only unique per table, so in the (practically
            // impossible) case that a made check shares a code with a poll, the check gets a fresh code first.
            migrationBuilder.Sql("""
                UPDATE user_checks uc
                SET share_code = left(uc.share_code, 40) || substr(md5(uc.id::text || clock_timestamp()::text), 1, 8)
                WHERE EXISTS (SELECT 1 FROM polls p WHERE p.share_code = uc.share_code);
                """);

            migrationBuilder.Sql("""
                INSERT INTO share_codes (code, content_type, poll_id, user_check_id, created_at)
                SELECT share_code, 'POLL', id, NULL, created_at FROM polls;
                """);

            migrationBuilder.Sql("""
                INSERT INTO share_codes (code, content_type, poll_id, user_check_id, created_at)
                SELECT share_code, 'CHECK', NULL, id, created_at FROM user_checks;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "share_codes");

            // Guest voter ids are not users, so they cannot satisfy the restored foreign key.
            migrationBuilder.Sql("DELETE FROM user_check_votes WHERE voter_role = 'GUEST';");

            migrationBuilder.DropColumn(
                name: "voter_role",
                table: "user_check_votes");

            migrationBuilder.AddForeignKey(
                name: "FK_user_check_votes_users_voter_id",
                table: "user_check_votes",
                column: "voter_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
