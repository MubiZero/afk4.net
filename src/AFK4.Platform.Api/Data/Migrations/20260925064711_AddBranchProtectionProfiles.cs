using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchProtectionProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_protection_profiles",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BlockRemovableStorage = table.Column<bool>(type: "boolean", nullable: false),
                    BlockBrowserDownloads = table.Column<bool>(type: "boolean", nullable: false),
                    BlockBrowserIncognito = table.Column<bool>(type: "boolean", nullable: false),
                    DisableRunDialog = table.Column<bool>(type: "boolean", nullable: false),
                    HiddenDrives = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    UrlBlocklistJson = table.Column<string>(type: "text", nullable: false),
                    BlockedWindowsJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_protection_profiles", x => x.BranchId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branch_protection_profiles_OrganizationId",
                table: "branch_protection_profiles",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_protection_profiles");
        }
    }
}
