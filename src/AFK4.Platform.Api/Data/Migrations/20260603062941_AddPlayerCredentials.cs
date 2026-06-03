using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_credentials",
                columns: table => new
                {
                    PlayerCredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    PhoneVerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_credentials", x => x.PlayerCredentialId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_credentials_OrganizationId_PlayerAccountId",
                table: "player_credentials",
                columns: new[] { "OrganizationId", "PlayerAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_player_credentials_PlayerAccountId",
                table: "player_credentials",
                column: "PlayerAccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_credentials");
        }
    }
}
