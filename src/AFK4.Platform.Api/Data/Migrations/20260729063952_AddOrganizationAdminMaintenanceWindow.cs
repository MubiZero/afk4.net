using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationAdminMaintenanceWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "OrganizationAdminMaintenanceWindowEnd",
                table: "branches",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(5, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OrganizationAdminMaintenanceWindowStart",
                table: "branches",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(4, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrganizationAdminMaintenanceWindowEnd",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "OrganizationAdminMaintenanceWindowStart",
                table: "branches");
        }
    }
}
