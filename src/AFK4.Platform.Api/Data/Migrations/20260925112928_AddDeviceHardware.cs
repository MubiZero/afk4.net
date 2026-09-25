using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceHardware : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_hardware",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentJson = table.Column<string>(type: "text", nullable: false),
                    CurrentFingerprint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedJson = table.Column<string>(type: "text", nullable: false),
                    AcceptedFingerprint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_hardware", x => x.DeviceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_hardware_OrganizationId_BranchId",
                table: "device_hardware",
                columns: new[] { "OrganizationId", "BranchId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_hardware");
        }
    }
}
