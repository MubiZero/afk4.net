using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_refresh_tokens",
                columns: table => new
                {
                    StaffRefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_refresh_tokens", x => x.StaffRefreshTokenId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_refresh_tokens_StaffUserId_ExpiresAtUtc",
                table: "staff_refresh_tokens",
                columns: new[] { "StaffUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_refresh_tokens_TokenHash",
                table: "staff_refresh_tokens",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_refresh_tokens");
        }
    }
}
