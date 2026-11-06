using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalityCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "personality_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Draft"),
                    required_votes = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    total_votes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_cards", x => x.id);
                    table.CheckConstraint("CK_personality_cards_completed_at_matches_status", "(\"status\" = 'Completed') = (\"completed_at\" IS NOT NULL)");
                    table.CheckConstraint("CK_personality_cards_required_votes_positive", "\"required_votes\" > 0");
                    table.CheckConstraint("CK_personality_cards_status", "\"status\" IN ('Draft', 'Collecting', 'Completed')");
                    table.CheckConstraint("CK_personality_cards_total_votes_in_range", "\"total_votes\" >= 0 AND \"total_votes\" <= \"required_votes\"");
                    table.ForeignKey(
                        name: "FK_personality_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personality_traits",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_traits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personality_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personality_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_votes", x => x.id);
                    table.ForeignKey(
                        name: "FK_personality_votes_personality_cards_personality_card_id",
                        column: x => x.personality_card_id,
                        principalTable: "personality_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_personality_votes_users_voter_user_id",
                        column: x => x.voter_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "personality_card_self_traits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personality_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trait_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_card_self_traits", x => x.id);
                    table.ForeignKey(
                        name: "FK_personality_card_self_traits_personality_cards_personality_~",
                        column: x => x.personality_card_id,
                        principalTable: "personality_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_personality_card_self_traits_personality_traits_trait_id",
                        column: x => x.trait_id,
                        principalTable: "personality_traits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personality_vote_traits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    personality_vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trait_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personality_vote_traits", x => x.id);
                    table.ForeignKey(
                        name: "FK_personality_vote_traits_personality_traits_trait_id",
                        column: x => x.trait_id,
                        principalTable: "personality_traits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personality_vote_traits_personality_votes_personality_vote_~",
                        column: x => x.personality_vote_id,
                        principalTable: "personality_votes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personality_card_self_traits_card_id_trait_id",
                table: "personality_card_self_traits",
                columns: new[] { "personality_card_id", "trait_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_card_self_traits_trait_id",
                table: "personality_card_self_traits",
                column: "trait_id");

            migrationBuilder.CreateIndex(
                name: "IX_personality_cards_user_id",
                table: "personality_cards",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_traits_display_order",
                table: "personality_traits",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "IX_personality_traits_name",
                table: "personality_traits",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_vote_traits_trait_id",
                table: "personality_vote_traits",
                column: "trait_id");

            migrationBuilder.CreateIndex(
                name: "IX_personality_vote_traits_vote_id_trait_id",
                table: "personality_vote_traits",
                columns: new[] { "personality_vote_id", "trait_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_card_id_voter_user_id",
                table: "personality_votes",
                columns: new[] { "personality_card_id", "voter_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personality_votes_voter_user_id",
                table: "personality_votes",
                column: "voter_user_id");

            // The predefined traits, with fixed ids. ON CONFLICT keeps this safe to run again.
            migrationBuilder.Sql("""
                INSERT INTO personality_traits (id, name, description, is_active, display_order) VALUES
                    (1, 'Confident', 'Self-assured and comfortable taking the lead.', true, 1),
                    (2, 'Introverted', 'Reflective and recharges through quiet time.', true, 2),
                    (3, 'Creative', 'Full of original ideas and imagination.', true, 3),
                    (4, 'Empathetic', 'Understands and cares about how others feel.', true, 4),
                    (5, 'Ambitious', 'Driven to achieve big goals.', true, 5),
                    (6, 'Humorous', 'Quick to make people laugh.', true, 6),
                    (7, 'Intelligent', 'Sharp, curious and quick to learn.', true, 7),
                    (8, 'Calm', 'Steady and composed under pressure.', true, 8),
                    (9, 'Adventurous', 'Loves trying new and exciting things.', true, 9),
                    (10, 'Reliable', 'Dependable and keeps their word.', true, 10)
                ON CONFLICT (id) DO NOTHING;
                """);

            // Backfill: one Draft card for every existing user. The unique user_id makes this idempotent.
            migrationBuilder.Sql("""
                INSERT INTO personality_cards (id, user_id, status, required_votes, total_votes, created_at, updated_at)
                SELECT gen_random_uuid(), u."Id", 'Draft', 5, 0, now(), now()
                FROM users u
                ON CONFLICT (user_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personality_card_self_traits");

            migrationBuilder.DropTable(
                name: "personality_vote_traits");

            migrationBuilder.DropTable(
                name: "personality_traits");

            migrationBuilder.DropTable(
                name: "personality_votes");

            migrationBuilder.DropTable(
                name: "personality_cards");
        }
    }
}
