using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    background_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    sticker = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checks", x => x.id);
                    table.CheckConstraint("CK_checks_background_color", "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                });

            migrationBuilder.CreateTable(
                name: "check_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_options", x => x.id);
                    table.UniqueConstraint("AK_check_options_id_check_id", x => new { x.id, x.check_id });
                    table.ForeignKey(
                        name: "FK_check_options_checks_check_id",
                        column: x => x.check_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "check_rotations",
                columns: table => new
                {
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_check_rotations", x => x.week_start);
                    table.ForeignKey(
                        name: "FK_check_rotations_checks_check_id",
                        column: x => x.check_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_checks", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_checks_checks_check_id",
                        column: x => x.check_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_checks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_check_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_check_votes", x => x.id);
                    table.CheckConstraint("CK_user_check_votes_reason_not_blank", "\"reason\" IS NULL OR char_length(btrim(\"reason\")) > 0");
                    table.ForeignKey(
                        name: "FK_user_check_votes_check_options_check_option_id_check_id",
                        columns: x => new { x.check_option_id, x.check_id },
                        principalTable: "check_options",
                        principalColumns: new[] { "id", "check_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_check_votes_user_checks_user_check_id",
                        column: x => x.user_check_id,
                        principalTable: "user_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_check_votes_users_voter_id",
                        column: x => x.voter_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_check_options_check_id_sort_order",
                table: "check_options",
                columns: new[] { "check_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_check_rotations_check_id",
                table: "check_rotations",
                column: "check_id");

            migrationBuilder.CreateIndex(
                name: "IX_checks_sequence_no",
                table: "checks",
                column: "sequence_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_check_option_id",
                table: "user_check_votes",
                column: "check_option_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_check_option_id_check_id",
                table: "user_check_votes",
                columns: new[] { "check_option_id", "check_id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_user_check_id_voter_id",
                table: "user_check_votes",
                columns: new[] { "user_check_id", "voter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_check_votes_voter_id",
                table: "user_check_votes",
                column: "voter_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_checks_check_id",
                table: "user_checks",
                column: "check_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_checks_share_code",
                table: "user_checks",
                column: "share_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_checks_user_id_check_id",
                table: "user_checks",
                columns: new[] { "user_id", "check_id" },
                unique: true);

            // Seed the initial weekly checks (sequence_no is the display and rotation order).
            migrationBuilder.Sql("""
                WITH seed(seq, title, question, color, sticker, opts) AS (VALUES
                    (1, 'Mirror Check', 'Do people like me, or just like having me around?', '#FFE0E8', 'No filters. Just truth.',
                        ARRAY['Genuinely like you', 'Like having you around', 'Both']),
                    (2, 'Trust Check', 'If I told you a secret, how long before someone else knows?', '#DDF3E4', 'Keep it real.',
                        ARRAY['Never', 'A week', 'Already told']),
                    (3, 'Vibe Check', 'When I walk in, does the energy go up?', '#ECE3FF', 'Read the room',
                        ARRAY['Up', 'Down']),
                    (4, 'Loyalty Check', 'Would you still pick me if it got awkward?', '#FFEFC2', 'Say it plain',
                        ARRAY['Always', 'Probably not'])
                ),
                inserted AS (
                    INSERT INTO checks (id, sequence_no, title, question, background_color, sticker, is_active, created_at, updated_at)
                    SELECT gen_random_uuid(), seq, title, question, color, sticker, true, now(), now() FROM seed
                    RETURNING id, sequence_no
                )
                INSERT INTO check_options (id, check_id, text, sort_order)
                SELECT gen_random_uuid(), inserted.id, o.text, o.ord::int
                FROM inserted
                JOIN seed ON seed.seq = inserted.sequence_no
                CROSS JOIN LATERAL unnest(seed.opts) WITH ORDINALITY AS o(text, ord);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "check_rotations");

            migrationBuilder.DropTable(
                name: "user_check_votes");

            migrationBuilder.DropTable(
                name: "check_options");

            migrationBuilder.DropTable(
                name: "user_checks");

            migrationBuilder.DropTable(
                name: "checks");
        }
    }
}
