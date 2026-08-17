using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliesFromMinuteOfDay",
                table: "tariffs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliesOnDaysMask",
                table: "tariffs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AppliesToMinuteOfDay",
                table: "tariffs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliesFromMinuteOfDay",
                table: "tariffs");

            migrationBuilder.DropColumn(
                name: "AppliesOnDaysMask",
                table: "tariffs");

            migrationBuilder.DropColumn(
                name: "AppliesToMinuteOfDay",
                table: "tariffs");
        }
    }
}
