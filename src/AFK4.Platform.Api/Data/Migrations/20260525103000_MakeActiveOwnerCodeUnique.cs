using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20260525103000_MakeActiveOwnerCodeUnique")]
    public partial class MakeActiveOwnerCodeUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_owner_codes_StaffUserId",
                table: "owner_codes");

            migrationBuilder.CreateIndex(
                name: "IX_owner_codes_StaffUserId",
                table: "owner_codes",
                column: "StaffUserId",
                unique: true,
                filter: "\"RevokedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_owner_codes_StaffUserId",
                table: "owner_codes");

            migrationBuilder.CreateIndex(
                name: "IX_owner_codes_StaffUserId",
                table: "owner_codes",
                column: "StaffUserId",
                filter: "\"RevokedAtUtc\" IS NULL");
        }
    }
}
