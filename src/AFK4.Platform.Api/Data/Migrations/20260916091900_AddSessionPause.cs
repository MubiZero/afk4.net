using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalPausedSeconds",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxSessionPauseMinutes",
                table: "branches",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "TotalPausedSeconds",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "MaxSessionPauseMinutes",
                table: "branches");
        }
    }
}
