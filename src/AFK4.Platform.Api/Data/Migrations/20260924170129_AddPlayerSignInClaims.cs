using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerSignInClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_sign_in_claims",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedeemedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_sign_in_claims", x => x.ClaimId);
                });

            migrationBuilder.CreateTable(
                name: "seating_code_attempt_counters",
                columns: table => new
                {
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    WindowStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seating_code_attempt_counters", x => new { x.Scope, x.ScopeId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_sign_in_claims_DeviceId_ExpiresAtUtc",
                table: "player_sign_in_claims",
                columns: new[] { "DeviceId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_player_sign_in_claims_PlatformPersonId_OrganizationId_Idemp~",
                table: "player_sign_in_claims",
                columns: new[] { "PlatformPersonId", "OrganizationId", "IdempotencyKeyHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_sign_in_claims");

            migrationBuilder.DropTable(
                name: "seating_code_attempt_counters");
        }
    }
}
