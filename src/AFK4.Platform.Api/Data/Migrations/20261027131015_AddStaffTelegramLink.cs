using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffTelegramLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "staff_users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramLinkCodeExpiresAtUtc",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramLinkCodeHash",
                table: "staff_users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TelegramLinkedAtUtc",
                table: "staff_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_users_TelegramChatId",
                table: "staff_users",
                column: "TelegramChatId",
                filter: "\"TelegramChatId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_users_TelegramLinkCodeHash",
                table: "staff_users",
                column: "TelegramLinkCodeHash",
                unique: true,
                filter: "\"TelegramLinkCodeHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_users_TelegramChatId",
                table: "staff_users");

            migrationBuilder.DropIndex(
                name: "IX_staff_users_TelegramLinkCodeHash",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkCodeExpiresAtUtc",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkCodeHash",
                table: "staff_users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkedAtUtc",
                table: "staff_users");
        }
    }
}
