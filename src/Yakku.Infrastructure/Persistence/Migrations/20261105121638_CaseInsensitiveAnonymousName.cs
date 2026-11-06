using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseInsensitiveAnonymousName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_AnonymousName",
                table: "user_profiles");

            migrationBuilder.AddColumn<string>(
                name: "AnonymousNameKey",
                table: "user_profiles",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(\"AnonymousName\")",
                stored: true);

            // The old index was case-sensitive. Should two names differ only by case, keep the oldest as it is and
            // give the others a short id suffix so the new case-insensitive unique index can be created. No-op when
            // there are none (all existing names are lower-case).
            migrationBuilder.Sql("""
                UPDATE user_profiles p
                SET "AnonymousName" = left(p."AnonymousName", 41) || '-' || left(p."Id"::text, 8)
                FROM (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY lower("AnonymousName") ORDER BY "CreatedAt", "Id") AS rn
                    FROM user_profiles
                ) d
                WHERE p."Id" = d."Id" AND d.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AnonymousName",
                table: "user_profiles",
                column: "AnonymousNameKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_AnonymousName",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "AnonymousNameKey",
                table: "user_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AnonymousName",
                table: "user_profiles",
                column: "AnonymousName",
                unique: true);
        }
    }
}
