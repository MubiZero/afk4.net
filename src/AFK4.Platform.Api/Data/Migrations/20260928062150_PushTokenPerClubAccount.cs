using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PushTokenPerClubAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_player_devices_PushToken",
                table: "player_devices");

            migrationBuilder.CreateIndex(
                name: "IX_player_devices_PushToken_PlayerAccountId",
                table: "player_devices",
                columns: new[] { "PushToken", "PlayerAccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_player_devices_PushToken_PlayerAccountId",
                table: "player_devices");

            migrationBuilder.CreateIndex(
                name: "IX_player_devices_PushToken",
                table: "player_devices",
                column: "PushToken",
                unique: true);
        }
    }
}
