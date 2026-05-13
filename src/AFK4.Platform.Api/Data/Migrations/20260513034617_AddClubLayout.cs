using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_seat_assignments",
                columns: table => new
                {
                    DeviceSeatAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetachedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_seat_assignments", x => x.DeviceSeatAssignmentId);
                });

            migrationBuilder.CreateTable(
                name: "seats",
                columns: table => new
                {
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seats", x => x.SeatId);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.ZoneId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_seat_assignments_DeviceId_DetachedAtUtc",
                table: "device_seat_assignments",
                columns: new[] { "DeviceId", "DetachedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_device_seat_assignments_OrganizationId_BranchId",
                table: "device_seat_assignments",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_device_seat_assignments_SeatId_DetachedAtUtc",
                table: "device_seat_assignments",
                columns: new[] { "SeatId", "DetachedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_seats_OrganizationId_BranchId_ZoneId_SortOrder",
                table: "seats",
                columns: new[] { "OrganizationId", "BranchId", "ZoneId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_zones_OrganizationId_BranchId_SortOrder",
                table: "zones",
                columns: new[] { "OrganizationId", "BranchId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_seat_assignments");

            migrationBuilder.DropTable(
                name: "seats");

            migrationBuilder.DropTable(
                name: "zones");
        }
    }
}
