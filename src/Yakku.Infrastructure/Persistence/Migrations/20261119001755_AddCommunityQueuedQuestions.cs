using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityQueuedQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_queued_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_queued_questions", x => x.id);
                    table.CheckConstraint("CK_community_queued_questions_question_not_blank", "char_length(btrim(\"question\")) > 0");
                    table.ForeignKey(
                        name: "FK_community_queued_questions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_queued_question_tags",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_queued_question_tags", x => new { x.question_id, x.tag });
                    table.CheckConstraint("CK_community_queued_question_tags_tag", "\"tag\" IN ('friendships', 'career', 'family', 'love', 'money')");
                    table.ForeignKey(
                        name: "FK_community_queued_question_tags_community_queued_questions_q~",
                        column: x => x.question_id,
                        principalTable: "community_queued_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_queued_question_tags_tag",
                table: "community_queued_question_tags",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "IX_community_queued_questions_user_id_created_at",
                table: "community_queued_questions",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_queued_question_tags");

            migrationBuilder.DropTable(
                name: "community_queued_questions");
        }
    }
}
