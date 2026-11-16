using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yakku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestClientInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserName",
                table: "guests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrowserVersion",
                table: "guests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CookiesEnabled",
                table: "guests",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "guests",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "guests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<IPAddress>(
                name: "IpAddress",
                table: "guests",
                type: "inet",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "guests",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsName",
                table: "guests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "guests",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrowserName",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "BrowserVersion",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "CookiesEnabled",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "OsName",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "guests");
        }
    }
}
