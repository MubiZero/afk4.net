using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPcSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShutdownAtMinuteOfDay",
                table: "branch_protection_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WakeAtMinuteOfDay",
                table: "branch_protection_profiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShutdownAtMinuteOfDay",
                table: "branch_protection_profiles");

            migrationBuilder.DropColumn(
                name: "WakeAtMinuteOfDay",
                table: "branch_protection_profiles");
        }
    }
}
