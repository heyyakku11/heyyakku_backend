using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UsePersonalityTraitGuidIds : Migration
    {
        // Trait number n (1 to 10) becomes b2e4d1a0-0000-4000-8000-00000000000n, the same ids as PersonalityTraitIds.
        // Existing self-assessments and votes keep pointing at the same traits.
        private const string ToGuid = "('b2e4d1a0-0000-4000-8000-' || lpad(\"{0}\"::text, 12, '0'))::uuid";
        private const string ToNumber = "right(\"{0}\"::text, 12)::integer";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ChangeIdType(migrationBuilder, "uuid", ToGuid);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ChangeIdType(migrationBuilder, "integer", ToNumber);
        }

        /// <summary>
        /// Changes the id column type of the traits and of both tables that reference it, in place. The foreign keys
        /// are dropped first because their column types would briefly disagree, then recreated unchanged.
        /// </summary>
        private static void ChangeIdType(MigrationBuilder migrationBuilder, string type, string expression)
        {
            migrationBuilder.Sql("""
                ALTER TABLE personality_card_self_traits DROP CONSTRAINT "FK_personality_card_self_traits_personality_traits_trait_id";
                ALTER TABLE personality_vote_traits DROP CONSTRAINT "FK_personality_vote_traits_personality_traits_trait_id";
                """);

            migrationBuilder.Sql(
                $"ALTER TABLE personality_traits ALTER COLUMN id TYPE {type} USING {string.Format(expression, "id")};");
            migrationBuilder.Sql(
                $"ALTER TABLE personality_card_self_traits ALTER COLUMN trait_id TYPE {type} USING {string.Format(expression, "trait_id")};");
            migrationBuilder.Sql(
                $"ALTER TABLE personality_vote_traits ALTER COLUMN trait_id TYPE {type} USING {string.Format(expression, "trait_id")};");

            migrationBuilder.Sql("""
                ALTER TABLE personality_card_self_traits ADD CONSTRAINT "FK_personality_card_self_traits_personality_traits_trait_id"
                    FOREIGN KEY (trait_id) REFERENCES personality_traits (id) ON DELETE RESTRICT;
                ALTER TABLE personality_vote_traits ADD CONSTRAINT "FK_personality_vote_traits_personality_traits_trait_id"
                    FOREIGN KEY (trait_id) REFERENCES personality_traits (id) ON DELETE RESTRICT;
                """);
        }
    }
}
