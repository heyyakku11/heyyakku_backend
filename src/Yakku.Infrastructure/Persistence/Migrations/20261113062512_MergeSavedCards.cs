using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeSavedCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: true),
                    check_id = table.Column<Guid>(type: "uuid", nullable: true),
                    saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_cards", x => x.id);
                    table.CheckConstraint("CK_saved_cards_card_type_matches_target", "(\"card_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"check_id\" IS NULL) OR (\"card_type\" = 'FUNCARD' AND \"check_id\" IS NOT NULL AND \"poll_id\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_saved_cards_checks_check_id",
                        column: x => x.check_id,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_cards_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_check_id",
                table: "saved_cards",
                column: "check_id",
                filter: "\"check_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_poll_id",
                table: "saved_cards",
                column: "poll_id",
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_check",
                table: "saved_cards",
                columns: new[] { "user_id", "check_id" },
                unique: true,
                filter: "\"check_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_poll",
                table: "saved_cards",
                columns: new[] { "user_id", "poll_id" },
                unique: true,
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_cards_user_saved_at",
                table: "saved_cards",
                columns: new[] { "user_id", "saved_at" });

            // Move every existing saved poll and saved fun card into the merged table before the old tables go.
            migrationBuilder.Sql(@"
                INSERT INTO saved_cards (id, user_id, card_type, poll_id, check_id, saved_at)
                SELECT ""Id"", ""UserId"", 'POLL', ""PollId"", NULL, ""SavedAt"" FROM saved_polls;

                INSERT INTO saved_cards (id, user_id, card_type, poll_id, check_id, saved_at)
                SELECT ""Id"", ""UserId"", 'FUNCARD', NULL, ""CheckId"", ""SavedAt"" FROM saved_fun_cards;");

            migrationBuilder.DropTable(
                name: "saved_fun_cards");

            migrationBuilder.DropTable(
                name: "saved_polls");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_fun_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_fun_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_fun_cards_checks_CheckId",
                        column: x => x.CheckId,
                        principalTable: "checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_fun_cards_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_polls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_polls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_polls_polls_PollId",
                        column: x => x.PollId,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_saved_polls_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedFunCards_CheckId",
                table: "saved_fun_cards",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFunCards_UserId_CheckId",
                table: "saved_fun_cards",
                columns: new[] { "UserId", "CheckId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedPolls_PollId",
                table: "saved_polls",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedPolls_UserId_PollId",
                table: "saved_polls",
                columns: new[] { "UserId", "PollId" },
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO saved_polls (""Id"", ""PollId"", ""UserId"", ""SavedAt"")
                SELECT id, poll_id, user_id, saved_at FROM saved_cards WHERE card_type = 'POLL';

                INSERT INTO saved_fun_cards (""Id"", ""CheckId"", ""UserId"", ""SavedAt"")
                SELECT id, check_id, user_id, saved_at FROM saved_cards WHERE card_type = 'FUNCARD';");

            migrationBuilder.DropTable(
                name: "saved_cards");
        }
    }
}
