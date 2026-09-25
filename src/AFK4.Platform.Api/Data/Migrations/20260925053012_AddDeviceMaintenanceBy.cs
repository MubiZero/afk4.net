using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceMaintenanceBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaintenanceByName",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaintenanceByStaffUserId",
                table: "devices",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaintenanceByName",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "MaintenanceByStaffUserId",
                table: "devices");
        }
    }
}
