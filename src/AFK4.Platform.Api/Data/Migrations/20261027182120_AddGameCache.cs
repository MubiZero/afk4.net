using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GameCacheReachable",
                table: "devices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameCacheAddress",
                table: "branches",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameCacheReachable",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GameCacheAddress",
                table: "branches");
        }
    }
}
