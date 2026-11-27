using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves the anonymous name onto <c>users</c>, adds <c>user_permissions</c> (one row per user) and drops
    /// <c>user_profiles</c>. Data is copied before anything is dropped: every user keeps their anonymous name, and
    /// their ask-back choice becomes <c>allow_ask_back</c>.
    /// </summary>
    public partial class UserPermissionsAndAnonymousNameOnUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The anonymous name on users. Every row first gets a unique placeholder so the unique index can be
            //    built, then the real name is copied over from the profile.
            migrationBuilder.AddColumn<string>(
                name: "AnonymousName",
                table: "users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE users SET "AnonymousName" = 'user-' || replace("Id"::text, '-', '');
                """);

            migrationBuilder.Sql("""
                UPDATE users u
                SET "AnonymousName" = p."AnonymousName"
                FROM user_profiles p
                WHERE p."UserId" = u."Id";
                """);

            // The lower-case key is generated from the name, so it is added after the names are in place.
            migrationBuilder.AddColumn<string>(
                name: "AnonymousNameKey",
                table: "users",
                type: "text",
                nullable: true,
                computedColumnSql: "lower(\"AnonymousName\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_AnonymousName",
                table: "users",
                column: "AnonymousNameKey",
                unique: true);

            // 2. The permissions table, with a row for every existing user. The ask-back choice is carried over; the
            //    other permissions start at their defaults (push off, SMS on, WhatsApp on).
            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allow_ask_back = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    push_notification = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sms = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    whatsapp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_permissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO user_permissions
                    (user_id, allow_ask_back, push_notification, sms, whatsapp, created_at, updated_at)
                SELECT u."Id",
                       COALESCE(p."AllowAskBack", TRUE),
                       FALSE,
                       TRUE,
                       TRUE,
                       u."CreatedAt",
                       COALESCE(p."UpdatedAt", u."UpdatedAt")
                FROM users u
                LEFT JOIN user_profiles p ON p."UserId" = u."Id";
                """);

            // 3. Nothing is left in the profile table that is not now on users or user_permissions.
            migrationBuilder.DropTable(
                name: "user_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowAskBack = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AnonymousName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AnonymousNameKey = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"AnonymousName\")", stored: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_profiles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AnonymousName",
                table: "user_profiles",
                column: "AnonymousNameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "user_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO user_profiles ("Id", "UserId", "AllowAskBack", "AnonymousName", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(),
                       u."Id",
                       COALESCE(perm.allow_ask_back, TRUE),
                       u."AnonymousName",
                       u."CreatedAt",
                       u."UpdatedAt"
                FROM users u
                LEFT JOIN user_permissions perm ON perm.user_id = u."Id";
                """);

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropIndex(
                name: "IX_Users_AnonymousName",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AnonymousNameKey",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AnonymousName",
                table: "users");
        }
    }
}
