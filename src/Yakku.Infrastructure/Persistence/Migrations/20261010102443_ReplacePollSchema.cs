using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePollSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous poll tables cannot be altered in place: status values, ClosedAt,
            // and stored counts do not match the new checks. Existing poll, option, vote,
            // and saved-poll rows are removed. SavedPolls itself is kept.
            migrationBuilder.DropForeignKey(
                name: "FK_SavedPolls_Polls_PollId",
                table: "SavedPolls");

            migrationBuilder.Sql("""DELETE FROM "SavedPolls";""");

            migrationBuilder.DropTable(name: "Votes");
            migrationBuilder.DropTable(name: "PollOptions");
            migrationBuilder.DropTable(name: "Polls");

            migrationBuilder.CreateTable(
                name: "polls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "DRAFT"),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PUBLIC"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polls", x => x.id);
                    table.CheckConstraint("CK_polls_content_type", "\"content_type\" IN ('QUESTION', 'IMAGE', 'VIDEO', 'AUDIO')");
                    table.CheckConstraint("CK_polls_status", "\"status\" IN ('DRAFT', 'ACTIVE', 'CLOSED', 'EXPIRED')");
                    table.CheckConstraint("CK_polls_visibility", "\"visibility\" IN ('PUBLIC', 'PRIVATE')");
                    table.CheckConstraint("CK_polls_question_not_blank", "char_length(btrim(\"question\")) > 0");
                    table.ForeignKey(
                        name: "FK_polls_Users_created_by",
                        column: x => x.created_by,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "poll_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_options", x => x.id);
                    table.UniqueConstraint("AK_poll_options_id_poll_id", x => new { x.id, x.poll_id });
                    table.CheckConstraint("CK_poll_options_text_not_blank", "char_length(btrim(\"option_text\")) > 0");
                    table.CheckConstraint("CK_poll_options_display_order_positive", "\"display_order\" > 0");
                    table.ForeignKey(
                        name: "FK_poll_options_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bucket_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    thumbnail_object_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_media", x => x.id);
                    table.CheckConstraint("CK_poll_media_bucket_not_blank", "char_length(btrim(\"bucket_name\")) > 0");
                    table.CheckConstraint("CK_poll_media_duration_positive", "\"duration_ms\" IS NULL OR \"duration_ms\" > 0");
                    table.CheckConstraint("CK_poll_media_file_size_positive", "\"file_size\" > 0");
                    table.CheckConstraint("CK_poll_media_media_type", "\"media_type\" IN ('IMAGE', 'VIDEO', 'AUDIO')");
                    table.CheckConstraint("CK_poll_media_mime_type_not_blank", "char_length(btrim(\"mime_type\")) > 0");
                    table.CheckConstraint("CK_poll_media_object_key_not_blank", "char_length(btrim(\"object_key\")) > 0");
                    table.CheckConstraint("CK_poll_media_thumbnail_not_blank", "\"thumbnail_object_key\" IS NULL OR char_length(btrim(\"thumbnail_object_key\")) > 0");
                    table.ForeignKey(
                        name: "FK_poll_media_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_option_id = table.Column<Guid>(type: "uuid", nullable: true),
                    custom_answer = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_votes", x => x.id);
                    table.CheckConstraint(
                        "CK_poll_votes_one_choice",
                        "(\"poll_option_id\" IS NOT NULL AND \"custom_answer\" IS NULL) OR (\"poll_option_id\" IS NULL AND \"custom_answer\" IS NOT NULL)");
                    table.CheckConstraint(
                        "CK_poll_votes_custom_answer_not_blank",
                        "\"custom_answer\" IS NULL OR char_length(btrim(\"custom_answer\")) > 0");
                    table.CheckConstraint(
                        "CK_poll_votes_reason_not_blank",
                        "\"reason\" IS NULL OR char_length(btrim(\"reason\")) > 0");
                    table.ForeignKey(
                        name: "FK_poll_votes_Users_voter_id",
                        column: x => x.voter_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_poll_votes_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // EF Core cannot mark this composite foreign key DEFERRABLE. MATCH SIMPLE still
            // allows a null poll_option_id. Deferring the RESTRICT check lets a poll delete
            // cascade votes and options in one transaction.
            migrationBuilder.Sql(
                """
                ALTER TABLE poll_votes
                    ADD CONSTRAINT "FK_poll_votes_poll_options_poll_option_id_poll_id"
                    FOREIGN KEY (poll_option_id, poll_id)
                    REFERENCES poll_options (id, poll_id)
                    ON DELETE RESTRICT
                    DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_polls_created_by_created_at",
                table: "polls",
                columns: new[] { "created_by", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_polls_active_public_created_at",
                table: "polls",
                column: "created_at",
                descending: new[] { true },
                filter: "\"status\" = 'ACTIVE' AND \"visibility\" = 'PUBLIC'");

            migrationBuilder.CreateIndex(
                name: "IX_polls_expires_at",
                table: "polls",
                column: "expires_at",
                filter: "\"expires_at\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_poll_options_poll_id_display_order",
                table: "poll_options",
                columns: new[] { "poll_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_options_poll_id_option_text",
                table: "poll_options",
                columns: new[] { "poll_id", "option_text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_media_poll_id",
                table: "poll_media",
                column: "poll_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_poll_id_voter_id",
                table: "poll_votes",
                columns: new[] { "poll_id", "voter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_voter_id",
                table: "poll_votes",
                column: "voter_id");

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_poll_option_id",
                table: "poll_votes",
                column: "poll_option_id",
                filter: "\"poll_option_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_poll_votes_poll_option_id_poll_id",
                table: "poll_votes",
                columns: new[] { "poll_option_id", "poll_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_SavedPolls_polls_PollId",
                table: "SavedPolls",
                column: "PollId",
                principalTable: "polls",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SavedPolls_polls_PollId",
                table: "SavedPolls");

            migrationBuilder.Sql("""DELETE FROM "SavedPolls";""");

            migrationBuilder.DropTable(name: "poll_votes");
            migrationBuilder.DropTable(name: "poll_media");
            migrationBuilder.DropTable(name: "poll_options");
            migrationBuilder.DropTable(name: "polls");

            migrationBuilder.CreateTable(
                name: "Polls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OptionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalVoteCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalWhyCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Polls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Polls_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PollOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoteCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PollOptions_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: true),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomOptionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.CheckConstraint("CK_Votes_OneVoter", "(\"UserId\" IS NOT NULL AND \"GuestId\" IS NULL) OR (\"UserId\" IS NULL AND \"GuestId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Votes_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Votes_PollOptions_PollOptionId",
                        column: x => x.PollOptionId,
                        principalTable: "PollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Votes_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Polls_CreatorId",
                table: "Polls",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_ShareToken",
                table: "Polls",
                column: "ShareToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Polls_Status_CreatedAt",
                table: "Polls",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PollOptions_PollId",
                table: "PollOptions",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_PollOptions_PollId_SortOrder",
                table: "PollOptions",
                columns: new[] { "PollId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_GuestId",
                table: "Votes",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollId",
                table: "Votes",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollId_GuestId",
                table: "Votes",
                columns: new[] { "PollId", "GuestId" },
                unique: true,
                filter: "\"GuestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollId_PollOptionId",
                table: "Votes",
                columns: new[] { "PollId", "PollOptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollId_UserId",
                table: "Votes",
                columns: new[] { "PollId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollOptionId",
                table: "Votes",
                column: "PollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId",
                table: "Votes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId_CreatedAt",
                table: "Votes",
                columns: new[] { "UserId", "CreatedAt" },
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedPolls_Polls_PollId",
                table: "SavedPolls",
                column: "PollId",
                principalTable: "Polls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
