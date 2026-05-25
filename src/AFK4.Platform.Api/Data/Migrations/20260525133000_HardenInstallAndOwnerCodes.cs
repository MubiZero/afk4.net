using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20260525133000_HardenInstallAndOwnerCodes")]
    public partial class HardenInstallAndOwnerCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DevicePublicKey",
                table: "devices",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: false,
                defaultValue: "");

            migrationBuilder.DropIndex(
                name: "IX_owner_codes_CodeHash",
                table: "owner_codes");

            migrationBuilder.CreateIndex(
                name: "IX_owner_codes_CodeHash",
                table: "owner_codes",
                column: "CodeHash",
                unique: true,
                filter: "\"RevokedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_owner_codes_CodeHash",
                table: "owner_codes");

            migrationBuilder.CreateIndex(
                name: "IX_owner_codes_CodeHash",
                table: "owner_codes",
                column: "CodeHash");

            migrationBuilder.DropColumn(
                name: "DevicePublicKey",
                table: "devices");
        }
    }
}
