using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignSchemaToDbDiagram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Polls_Categories_CategoryId",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_CategoryId_Status_CreatedAt",
                table: "Polls");

            migrationBuilder.AddColumn<Guid>(
                name: "AdminId",
                table: "SystemLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                table: "SystemLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceType",
                table: "SystemLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowComments",
                table: "Polls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Polls",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedReason",
                table: "Polls",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Categories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PermissionsList = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalyticsMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MetricDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Value = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalyticsMetrics_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AnalyticsMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EditedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comments_Comments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Comments_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Comments_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PollAnalytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalVotes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UniqueVoters = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CommentCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    AverageTimeToVote = table.Column<int>(type: "integer", nullable: true),
                    GuestVotesCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UserVotesCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Views = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Shares = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReportCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollAnalytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollAnalytics_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PollCategories_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "PollCategories" ("Id", "PollId", "CategoryId", "AddedAt")
                SELECT gen_random_uuid(), "Id", "CategoryId", NOW() AT TIME ZONE 'utc'
                FROM "Polls"
                WHERE "CategoryId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Polls");

            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AdminRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Active"),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Admins_AdminRoles_AdminRoleId",
                        column: x => x.AdminRoleId,
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<IPAddress>(type: "inet", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminSessions_Admins_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<IPAddress>(type: "inet", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Admins_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedByGuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ActionTakenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.CheckConstraint("CK_Reports_OneReporter", "(\"ReportedByUserId\" IS NOT NULL AND \"ReportedByGuestId\" IS NULL) OR (\"ReportedByUserId\" IS NULL AND \"ReportedByGuestId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Reports_Admins_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "Admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Guests_ReportedByGuestId",
                        column: x => x.ReportedByGuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_AdminId",
                table: "SystemLogs",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_Status_CreatedAt",
                table: "Polls",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DeletedBy",
                table: "Categories",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AdminRoles_Name",
                table: "AdminRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Admins_AdminRoleId",
                table: "Admins",
                column: "AdminRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Email",
                table: "Admins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_AdminId",
                table: "AdminSessions",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminSessions_RefreshTokenHash",
                table: "AdminSessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsMetrics_CategoryId",
                table: "AnalyticsMetrics",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsMetrics_MetricType_MetricDate",
                table: "AnalyticsMetrics",
                columns: new[] { "MetricType", "MetricDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsMetrics_UserId",
                table: "AnalyticsMetrics",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AdminId",
                table: "AuditLogs",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ResourceType_ResourceId",
                table: "AuditLogs",
                columns: new[] { "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PollId",
                table: "Comments",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PollId_CreatedAt",
                table: "Comments",
                columns: new[] { "PollId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PollAnalytics_PollId",
                table: "PollAnalytics",
                column: "PollId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollCategories_CategoryId",
                table: "PollCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PollCategories_PollId_CategoryId",
                table: "PollCategories",
                columns: new[] { "PollId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedByGuestId",
                table: "Reports",
                column: "ReportedByGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedByUserId",
                table: "Reports",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ResourceType_ResourceId",
                table: "Reports",
                columns: new[] { "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReviewedBy",
                table: "Reports",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status",
                table: "Reports",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Admins_DeletedBy",
                table: "Categories",
                column: "DeletedBy",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemLogs_Admins_AdminId",
                table: "SystemLogs",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Admins_DeletedBy",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemLogs_Admins_AdminId",
                table: "SystemLogs");

            migrationBuilder.DropTable(
                name: "AdminSessions");

            migrationBuilder.DropTable(
                name: "AnalyticsMetrics");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "PollAnalytics");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Polls",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Polls" p
                SET "CategoryId" = pc."CategoryId"
                FROM (
                    SELECT DISTINCT ON ("PollId") "PollId", "CategoryId"
                    FROM "PollCategories"
                    ORDER BY "PollId", "AddedAt"
                ) pc
                WHERE p."Id" = pc."PollId";
                """);

            migrationBuilder.DropTable(
                name: "PollCategories");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "AdminRoles");

            migrationBuilder.DropIndex(
                name: "IX_SystemLogs_AdminId",
                table: "SystemLogs");

            migrationBuilder.DropIndex(
                name: "IX_Polls_Status_CreatedAt",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Categories_DeletedBy",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "AdminId",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "ResourceType",
                table: "SystemLogs");

            migrationBuilder.DropColumn(
                name: "AllowComments",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "DeletedReason",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Polls");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_CategoryId_Status_CreatedAt",
                table: "Polls",
                columns: new[] { "CategoryId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Polls_Categories_CategoryId",
                table: "Polls",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
