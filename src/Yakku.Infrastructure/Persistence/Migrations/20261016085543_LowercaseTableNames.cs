using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LowercaseTableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdminSessions_Admins_AdminId",
                table: "AdminSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Users_UserId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_media_upload_sessions_Guests_guest_id",
                table: "media_upload_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_media_upload_sessions_Users_user_id",
                table: "media_upload_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationPreferences_Users_UserId",
                table: "NotificationPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_poll_votes_Users_voter_id",
                table: "poll_votes");

            migrationBuilder.DropForeignKey(
                name: "FK_polls_Users_created_by",
                table: "polls");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedPolls_Users_UserId",
                table: "SavedPolls");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedPolls_polls_PollId",
                table: "SavedPolls");

            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Users_UserId",
                table: "UserProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_Devices_DeviceId",
                table: "UserSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_Users_UserId",
                table: "UserSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Guests",
                table: "Guests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Devices",
                table: "Devices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Admins",
                table: "Admins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSessions",
                table: "UserSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserProfiles",
                table: "UserProfiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SavedPolls",
                table: "SavedPolls");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NotificationPreferences",
                table: "NotificationPreferences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AdminSessions",
                table: "AdminSessions");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "Notifications",
                newName: "notifications");

            migrationBuilder.RenameTable(
                name: "Guests",
                newName: "guests");

            migrationBuilder.RenameTable(
                name: "Devices",
                newName: "devices");

            migrationBuilder.RenameTable(
                name: "Admins",
                newName: "admins");

            migrationBuilder.RenameTable(
                name: "UserSessions",
                newName: "user_sessions");

            migrationBuilder.RenameTable(
                name: "UserProfiles",
                newName: "user_profiles");

            migrationBuilder.RenameTable(
                name: "SavedPolls",
                newName: "saved_polls");

            migrationBuilder.RenameTable(
                name: "NotificationPreferences",
                newName: "notification_preferences");

            migrationBuilder.RenameTable(
                name: "AdminSessions",
                newName: "admin_sessions");

            migrationBuilder.RenameIndex(
                name: "IX_UserSessions_DeviceId",
                table: "user_sessions",
                newName: "IX_user_sessions_DeviceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notifications",
                table: "notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_guests",
                table: "guests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_devices",
                table: "devices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_admins",
                table: "admins",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_sessions",
                table: "user_sessions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_profiles",
                table: "user_profiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_saved_polls",
                table: "saved_polls",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_notification_preferences",
                table: "notification_preferences",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_admin_sessions",
                table: "admin_sessions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_admin_sessions_admins_AdminId",
                table: "admin_sessions",
                column: "AdminId",
                principalTable: "admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_users_UserId",
                table: "devices",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_media_upload_sessions_guests_guest_id",
                table: "media_upload_sessions",
                column: "guest_id",
                principalTable: "guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_media_upload_sessions_users_user_id",
                table: "media_upload_sessions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_notification_preferences_users_UserId",
                table: "notification_preferences",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_poll_votes_users_voter_id",
                table: "poll_votes",
                column: "voter_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_polls_users_created_by",
                table: "polls",
                column: "created_by",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_saved_polls_polls_PollId",
                table: "saved_polls",
                column: "PollId",
                principalTable: "polls",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_saved_polls_users_UserId",
                table: "saved_polls",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_profiles_users_UserId",
                table: "user_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_sessions_devices_DeviceId",
                table: "user_sessions",
                column: "DeviceId",
                principalTable: "devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_user_sessions_users_UserId",
                table: "user_sessions",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_admin_sessions_admins_AdminId",
                table: "admin_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_users_UserId",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_media_upload_sessions_guests_guest_id",
                table: "media_upload_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_media_upload_sessions_users_user_id",
                table: "media_upload_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_notification_preferences_users_UserId",
                table: "notification_preferences");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_users_UserId",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_poll_votes_users_voter_id",
                table: "poll_votes");

            migrationBuilder.DropForeignKey(
                name: "FK_polls_users_created_by",
                table: "polls");

            migrationBuilder.DropForeignKey(
                name: "FK_saved_polls_polls_PollId",
                table: "saved_polls");

            migrationBuilder.DropForeignKey(
                name: "FK_saved_polls_users_UserId",
                table: "saved_polls");

            migrationBuilder.DropForeignKey(
                name: "FK_user_profiles_users_UserId",
                table: "user_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_user_sessions_devices_DeviceId",
                table: "user_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_user_sessions_users_UserId",
                table: "user_sessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_notifications",
                table: "notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_guests",
                table: "guests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_devices",
                table: "devices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_admins",
                table: "admins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_sessions",
                table: "user_sessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_profiles",
                table: "user_profiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_saved_polls",
                table: "saved_polls");

            migrationBuilder.DropPrimaryKey(
                name: "PK_notification_preferences",
                table: "notification_preferences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_admin_sessions",
                table: "admin_sessions");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "notifications",
                newName: "Notifications");

            migrationBuilder.RenameTable(
                name: "guests",
                newName: "Guests");

            migrationBuilder.RenameTable(
                name: "devices",
                newName: "Devices");

            migrationBuilder.RenameTable(
                name: "admins",
                newName: "Admins");

            migrationBuilder.RenameTable(
                name: "user_sessions",
                newName: "UserSessions");

            migrationBuilder.RenameTable(
                name: "user_profiles",
                newName: "UserProfiles");

            migrationBuilder.RenameTable(
                name: "saved_polls",
                newName: "SavedPolls");

            migrationBuilder.RenameTable(
                name: "notification_preferences",
                newName: "NotificationPreferences");

            migrationBuilder.RenameTable(
                name: "admin_sessions",
                newName: "AdminSessions");

            migrationBuilder.RenameIndex(
                name: "IX_user_sessions_DeviceId",
                table: "UserSessions",
                newName: "IX_UserSessions_DeviceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Notifications",
                table: "Notifications",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Guests",
                table: "Guests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Devices",
                table: "Devices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Admins",
                table: "Admins",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSessions",
                table: "UserSessions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserProfiles",
                table: "UserProfiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SavedPolls",
                table: "SavedPolls",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NotificationPreferences",
                table: "NotificationPreferences",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AdminSessions",
                table: "AdminSessions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AdminSessions_Admins_AdminId",
                table: "AdminSessions",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Users_UserId",
                table: "Devices",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_media_upload_sessions_Guests_guest_id",
                table: "media_upload_sessions",
                column: "guest_id",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_media_upload_sessions_Users_user_id",
                table: "media_upload_sessions",
                column: "user_id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationPreferences_Users_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_poll_votes_Users_voter_id",
                table: "poll_votes",
                column: "voter_id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_polls_Users_created_by",
                table: "polls",
                column: "created_by",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedPolls_Users_UserId",
                table: "SavedPolls",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedPolls_polls_PollId",
                table: "SavedPolls",
                column: "PollId",
                principalTable: "polls",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Users_UserId",
                table: "UserProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_Devices_DeviceId",
                table: "UserSessions",
                column: "DeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_Users_UserId",
                table: "UserSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
