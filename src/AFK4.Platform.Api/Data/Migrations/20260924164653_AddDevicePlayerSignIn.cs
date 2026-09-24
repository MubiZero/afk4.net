using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicePlayerSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "platform_person_refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeviceSignedInAtUtc",
                table: "platform_person_refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "platform_person_access_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeviceSignedInAtUtc",
                table: "platform_person_access_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlayerSignInFailedCount",
                table: "devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlayerSignInWindowStartedAtUtc",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_person_refresh_tokens_DeviceId",
                table: "platform_person_refresh_tokens",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_person_access_tokens_DeviceId",
                table: "platform_person_access_tokens",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_platform_person_refresh_tokens_DeviceId",
                table: "platform_person_refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_platform_person_access_tokens_DeviceId",
                table: "platform_person_access_tokens");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "platform_person_refresh_tokens");

            migrationBuilder.DropColumn(
                name: "DeviceSignedInAtUtc",
                table: "platform_person_refresh_tokens");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "platform_person_access_tokens");

            migrationBuilder.DropColumn(
                name: "DeviceSignedInAtUtc",
                table: "platform_person_access_tokens");

            migrationBuilder.DropColumn(
                name: "PlayerSignInFailedCount",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "PlayerSignInWindowStartedAtUtc",
                table: "devices");
        }
    }
}
