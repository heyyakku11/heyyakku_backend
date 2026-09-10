using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Polls" WHERE "CreatorId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot make Polls.CreatorId NOT NULL: one or more polls have a null CreatorId.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM "UserProfiles" WHERE length("DisplayName") > 50) THEN
                        RAISE EXCEPTION 'Cannot shrink UserProfiles.DisplayName to varchar(50): one or more names exceed 50 characters.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "SystemLogs" sl
                        WHERE sl."UserId" IS NOT NULL
                          AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = sl."UserId")) THEN
                        RAISE EXCEPTION 'Cannot add SystemLogs.UserId foreign key: orphan UserId values exist.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "SystemLogs" sl
                        WHERE sl."GuestId" IS NOT NULL
                          AND NOT EXISTS (SELECT 1 FROM "Guests" g WHERE g."Id" = sl."GuestId")) THEN
                        RAISE EXCEPTION 'Cannot add SystemLogs.GuestId foreign key: orphan GuestId values exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Users" SET "Status" = 'Disabled' WHERE "Status" IN ('Suspended', 'Deleted');
                UPDATE "Polls" SET "Status" = 'Closed' WHERE "Status" = 'Expired';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Polls_Users_CreatorId",
                table: "Polls");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Polls_PollId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_GuestId_PollId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Guests_TokenHash",
                table: "Guests");

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "UserSessions",
                newName: "RefreshTokenHash");

            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "Guests",
                newName: "GuestTokenHash");

            migrationBuilder.RenameColumn(
                name: "Level",
                table: "SystemLogs",
                newName: "Severity");

            migrationBuilder.RenameColumn(
                name: "Position",
                table: "PollOptions",
                newName: "SortOrder");

            migrationBuilder.RenameIndex(
                name: "IX_PollOptions_PollId_Position",
                table: "PollOptions",
                newName: "IX_PollOptions_PollId_SortOrder");

            migrationBuilder.DropColumn(
                name: "TokenSalt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "SystemLogs");

            migrationBuilder.AlterColumn<string>(
                name: "RefreshTokenHash",
                table: "UserSessions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "GuestTokenHash",
                table: "Guests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Votes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GuestId",
                table: "Votes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "CustomOptionText",
                table: "Votes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "Votes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Votes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "UserSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Users" SET "UpdatedAt" = "CreatedAt" WHERE "UpdatedAt" IS NULL;""");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "UserProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<Guid>(
                name: "AvatarImageId",
                table: "UserProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "SystemLogs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "SystemLogs"
                SET "Metadata" = CASE
                    WHEN "Details" IS NULL OR btrim("Details") = '' THEN NULL
                    WHEN left(btrim("Details"), 1) IN ('{', '[') THEN "Details"::jsonb
                    ELSE to_jsonb("Details")
                END;
                """);

            migrationBuilder.DropColumn(
                name: "Details",
                table: "SystemLogs");

            migrationBuilder.Sql(
                """
                UPDATE "SystemLogs" SET "Severity" = 'Info' WHERE "Severity" = 'Information';

                UPDATE "SystemLogs" SET "EventType" = CASE "EventType"
                    WHEN 'OtpRequested' THEN 'Authentication'
                    WHEN 'OtpInvalid' THEN 'Authentication'
                    WHEN 'UserRegistered' THEN 'Authentication'
                    WHEN 'UserLoggedIn' THEN 'Authentication'
                    WHEN 'SessionRefreshed' THEN 'Authentication'
                    WHEN 'SessionRevoked' THEN 'Authentication'
                    WHEN 'PollCreated' THEN 'Poll'
                    WHEN 'VoteCast' THEN 'Vote'
                    WHEN 'VoteRejectedAlreadyVoted' THEN 'Vote'
                    WHEN 'UnhandledException' THEN 'System'
                    ELSE 'System'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "SystemLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "SystemLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<IPAddress>(
                name: "IpAddress",
                table: "SystemLogs",
                type: "inet",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "SystemLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatorId",
                table: "Polls",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Polls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionType",
                table: "Polls",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Text");

            migrationBuilder.Sql("""ALTER TABLE "Polls" ALTER COLUMN "OptionType" DROP DEFAULT;""");

            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "Polls",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Polls"
                SET "ShareToken" = replace(gen_random_uuid()::text, '-', '')
                WHERE "ShareToken" IS NULL OR "ShareToken" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ShareToken",
                table: "Polls",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Polls" SET "UpdatedAt" = "CreatedAt" WHERE "UpdatedAt" IS NULL;""");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "PollOptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "PollOptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "PollOptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PollOptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "PollOptions" AS po
                SET "CreatedAt" = p."CreatedAt",
                    "UpdatedAt" = p."CreatedAt"
                FROM "Polls" p
                WHERE po."PollId" = p."Id";
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PollOptions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "PollOptions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastSeenAt",
                table: "Guests",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Guests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstallationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PushToken = table.Column<string>(type: "text", nullable: true),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeviceModel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppBuild = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NotificationPermission = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublicId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    SecureUrl = table.Column<string>(type: "text", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PushEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PollActivityEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    OffersEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AlertsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NormalEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Data = table.Column<string>(type: "jsonb", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_GuestId",
                table: "Votes",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_ImageId",
                table: "Votes",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollId_GuestId",
                table: "Votes",
                columns: new[] { "PollId", "GuestId" },
                unique: true,
                filter: "\"GuestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollId_UserId",
                table: "Votes",
                columns: new[] { "PollId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId",
                table: "Votes",
                column: "UserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_OneVoter",
                table: "Votes",
                sql: "(\"UserId\" IS NOT NULL AND \"GuestId\" IS NULL) OR (\"UserId\" IS NULL AND \"GuestId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_DeviceId",
                table: "UserSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_RefreshTokenHash",
                table: "UserSessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AvatarImageId",
                table: "UserProfiles",
                column: "AvatarImageId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_EventType",
                table: "SystemLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_GuestId",
                table: "SystemLogs",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_UserId",
                table: "SystemLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_CategoryId",
                table: "Polls",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_ShareToken",
                table: "Polls",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Polls_Status",
                table: "Polls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PollOptions_ImageId",
                table: "PollOptions",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_GuestTokenHash",
                table: "Guests",
                column: "GuestTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_InstallationId",
                table: "Devices",
                column: "InstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "Devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_PublicId",
                table: "Images",
                column: "PublicId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ImageId",
                table: "Notifications",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.AddForeignKey(
                name: "FK_PollOptions_Images_ImageId",
                table: "PollOptions",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Polls_Categories_CategoryId",
                table: "Polls",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Polls_Users_CreatorId",
                table: "Polls",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemLogs_Guests_GuestId",
                table: "SystemLogs",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemLogs_Users_UserId",
                table: "SystemLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Images_AvatarImageId",
                table: "UserProfiles",
                column: "AvatarImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_Devices_DeviceId",
                table: "UserSessions",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Images_ImageId",
                table: "Votes",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Polls_PollId",
                table: "Votes",
                column: "PollId",
                principalTable: "Polls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Users_UserId",
                table: "Votes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PollOptions_Images_ImageId",
                table: "PollOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Polls_Categories_CategoryId",
                table: "Polls");

            migrationBuilder.DropForeignKey(
                name: "FK_Polls_Users_CreatorId",
                table: "Polls");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemLogs_Guests_GuestId",
                table: "SystemLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemLogs_Users_UserId",
                table: "SystemLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Images_AvatarImageId",
                table: "UserProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_Devices_DeviceId",
                table: "UserSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Images_ImageId",
                table: "Votes");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Polls_PollId",
                table: "Votes");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Users_UserId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Votes_GuestId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_ImageId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_PollId_GuestId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_PollId_UserId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_UserId",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_OneVoter",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_DeviceId",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_RefreshTokenHash",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_AvatarImageId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SystemLogs_EventType",
                table: "SystemLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemLogs_GuestId",
                table: "SystemLogs");

            migrationBuilder.DropIndex(
                name: "IX_SystemLogs_UserId",
                table: "SystemLogs");

            migrationBuilder.DropIndex(
                name: "IX_Polls_CategoryId",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_ShareToken",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_Status",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_PollOptions_ImageId",
                table: "PollOptions");

            migrationBuilder.DropIndex(
                name: "IX_Guests_GuestTokenHash",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvatarImageId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "OptionType",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Guests");

            migrationBuilder.RenameColumn(
                name: "RefreshTokenHash",
                table: "UserSessions",
                newName: "TokenHash");

            migrationBuilder.RenameColumn(
                name: "GuestTokenHash",
                table: "Guests",
                newName: "TokenHash");

            migrationBuilder.RenameColumn(
                name: "Severity",
                table: "SystemLogs",
                newName: "Level");

            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "PollOptions",
                newName: "Position");

            migrationBuilder.RenameIndex(
                name: "IX_PollOptions_PollId_SortOrder",
                table: "PollOptions",
                newName: "IX_PollOptions_PollId_Position");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Votes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GuestId",
                table: "Votes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CustomOptionText",
                table: "Votes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "UserSessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "TokenSalt",
                table: "UserSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "UserProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "UserProfiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "SystemLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "SystemLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "SystemLogs",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "SystemLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatorId",
                table: "Polls",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "PollOptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastSeenAt",
                table: "Guests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "Guests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_GuestId_PollId",
                table: "Votes",
                columns: new[] { "GuestId", "PollId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_TokenHash",
                table: "Guests",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Polls_Users_CreatorId",
                table: "Polls",
                column: "CreatorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Polls_PollId",
                table: "Votes",
                column: "PollId",
                principalTable: "Polls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
