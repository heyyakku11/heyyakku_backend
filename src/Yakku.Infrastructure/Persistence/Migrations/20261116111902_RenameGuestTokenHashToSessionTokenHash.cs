using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameGuestTokenHashToSessionTokenHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GuestTokenHash",
                table: "guests",
                newName: "SessionTokenHash");

            migrationBuilder.RenameIndex(
                name: "IX_Guests_GuestTokenHash",
                table: "guests",
                newName: "IX_Guests_SessionTokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SessionTokenHash",
                table: "guests",
                newName: "GuestTokenHash");

            migrationBuilder.RenameIndex(
                name: "IX_Guests_SessionTokenHash",
                table: "guests",
                newName: "IX_Guests_GuestTokenHash");
        }
    }
}
