using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuiltInGameCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GameCacheServing",
                table: "devices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GameCacheStoredBytes",
                table: "devices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GameCacheDisabled",
                table: "branches",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameCacheServing",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GameCacheStoredBytes",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GameCacheDisabled",
                table: "branches");
        }
    }
}
