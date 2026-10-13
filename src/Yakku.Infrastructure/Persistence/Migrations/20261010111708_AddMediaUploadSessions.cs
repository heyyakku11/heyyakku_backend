using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "media_upload_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    poll_id = table.Column<Guid>(type: "uuid", nullable: true),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bucket_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expected_file_size = table.Column<long>(type: "bigint", nullable: true),
                    actual_file_size = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    selection_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attached_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    purged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_upload_sessions", x => x.id);
                    table.CheckConstraint("CK_media_upload_sessions_actual_size_positive", "\"actual_file_size\" IS NULL OR \"actual_file_size\" > 0");
                    table.CheckConstraint("CK_media_upload_sessions_bucket_not_blank", "char_length(btrim(\"bucket_name\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_expected_size_positive", "\"expected_file_size\" IS NULL OR \"expected_file_size\" > 0");
                    table.CheckConstraint("CK_media_upload_sessions_failure_reason_not_blank", "\"failure_reason\" IS NULL OR char_length(btrim(\"failure_reason\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_media_type", "\"media_type\" IN ('IMAGE', 'VIDEO', 'AUDIO')");
                    table.CheckConstraint("CK_media_upload_sessions_mime_type_not_blank", "char_length(btrim(\"mime_type\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_object_key_not_blank", "char_length(btrim(\"object_key\")) > 0");
                    table.CheckConstraint("CK_media_upload_sessions_selection_version_positive", "\"selection_version\" > 0");
                    table.CheckConstraint("CK_media_upload_sessions_status", "\"status\" IN ('PENDING', 'UPLOADING', 'UPLOADED', 'ATTACHED', 'CANCELLED', 'FAILED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "FK_media_upload_sessions_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_media_upload_sessions_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "polls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_object_key",
                table: "media_upload_sessions",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_poll_id",
                table: "media_upload_sessions",
                column: "poll_id",
                filter: "\"poll_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_media_upload_sessions_user_id_status_expires_at",
                table: "media_upload_sessions",
                columns: new[] { "user_id", "status", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_upload_sessions");
        }
    }
}
