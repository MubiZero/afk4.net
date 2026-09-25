using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShellSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClubRules",
                table: "branch_protection_profiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdleShutdownMinutes",
                table: "branch_protection_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LaunchOnSessionStart",
                table: "branch_games",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClubRules",
                table: "branch_protection_profiles");

            migrationBuilder.DropColumn(
                name: "IdleShutdownMinutes",
                table: "branch_protection_profiles");

            migrationBuilder.DropColumn(
                name: "LaunchOnSessionStart",
                table: "branch_games");
        }
    }
}
