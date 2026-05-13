using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceInstalledApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_installed_apps",
                columns: table => new
                {
                    DeviceInstalledAppId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Version = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Publisher = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    InstallLocation = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    InstalledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_installed_apps", x => x.DeviceInstalledAppId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_installed_apps_DeviceId_DisplayName",
                table: "device_installed_apps",
                columns: new[] { "DeviceId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_device_installed_apps_OrganizationId_BranchId_DeviceId",
                table: "device_installed_apps",
                columns: new[] { "OrganizationId", "BranchId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_device_installed_apps_ReportedAtUtc",
                table: "device_installed_apps",
                column: "ReportedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_installed_apps");
        }
    }
}
