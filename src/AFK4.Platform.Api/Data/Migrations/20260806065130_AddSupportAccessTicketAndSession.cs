using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportAccessTicketAndSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "SessionTokenHash",
                table: "platform_support_access_grants",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "TicketHash",
                table: "platform_support_access_grants",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TicketUsedAtUtc",
                table: "platform_support_access_grants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_support_access_grants_SessionTokenHash",
                table: "platform_support_access_grants",
                column: "SessionTokenHash",
                unique: true,
                filter: "\"SessionTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_platform_support_access_grants_TicketHash",
                table: "platform_support_access_grants",
                column: "TicketHash",
                unique: true,
                filter: "\"TicketHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_platform_support_access_grants_SessionTokenHash",
                table: "platform_support_access_grants");

            migrationBuilder.DropIndex(
                name: "IX_platform_support_access_grants_TicketHash",
                table: "platform_support_access_grants");

            migrationBuilder.DropColumn(
                name: "SessionTokenHash",
                table: "platform_support_access_grants");

            migrationBuilder.DropColumn(
                name: "TicketHash",
                table: "platform_support_access_grants");

            migrationBuilder.DropColumn(
                name: "TicketUsedAtUtc",
                table: "platform_support_access_grants");
        }
    }
}
