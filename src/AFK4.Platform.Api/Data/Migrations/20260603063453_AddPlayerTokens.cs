using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_access_tokens",
                columns: table => new
                {
                    PlayerAccessTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_access_tokens", x => x.PlayerAccessTokenId);
                });

            migrationBuilder.CreateTable(
                name: "player_refresh_tokens",
                columns: table => new
                {
                    PlayerRefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_refresh_tokens", x => x.PlayerRefreshTokenId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_access_tokens_PlayerAccountId_ExpiresAtUtc",
                table: "player_access_tokens",
                columns: new[] { "PlayerAccountId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_player_access_tokens_TokenHash",
                table: "player_access_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_player_refresh_tokens_PlayerAccountId_ExpiresAtUtc",
                table: "player_refresh_tokens",
                columns: new[] { "PlayerAccountId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_player_refresh_tokens_TokenHash",
                table: "player_refresh_tokens",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_access_tokens");

            migrationBuilder.DropTable(
                name: "player_refresh_tokens");
        }
    }
}
