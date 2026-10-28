using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerOutageResume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PauseReason",
                table: "sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnBatterySinceUtc",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_power_losses",
                columns: table => new
                {
                    DevicePowerLossId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Compensated = table.Column<bool>(type: "boolean", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnedMinutes = table.Column<int>(type: "integer", nullable: false),
                    ReportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_power_losses", x => x.DevicePowerLossId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_power_losses_BranchId_FromUtc",
                table: "device_power_losses",
                columns: new[] { "BranchId", "FromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_device_power_losses_DeviceId_FromUtc",
                table: "device_power_losses",
                columns: new[] { "DeviceId", "FromUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_power_losses");

            migrationBuilder.DropColumn(
                name: "PauseReason",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "OnBatterySinceUtc",
                table: "devices");
        }
    }
}
