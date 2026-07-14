using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class VersionReservationsAndLinkSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StartedSessionId",
                table: "reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_StartedSessionId",
                table: "reservations",
                column: "StartedSessionId",
                unique: true,
                filter: "\"StartedSessionId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_reservations_sessions_StartedSessionId",
                table: "reservations",
                column: "StartedSessionId",
                principalTable: "sessions",
                principalColumn: "SessionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reservations_sessions_StartedSessionId",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_StartedSessionId",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "StartedSessionId",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "reservations");
        }
    }
}
