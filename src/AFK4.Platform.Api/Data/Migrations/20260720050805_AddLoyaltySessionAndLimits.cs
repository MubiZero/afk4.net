using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltySessionAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CashbackCapMinorUnits",
                table: "organization_loyalty_settings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MinimumSourceMinorUnits",
                table: "organization_loyalty_settings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "SessionEnabled",
                table: "organization_loyalty_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SessionPercentBasisPoints",
                table: "organization_loyalty_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashbackCapMinorUnits",
                table: "organization_loyalty_settings");

            migrationBuilder.DropColumn(
                name: "MinimumSourceMinorUnits",
                table: "organization_loyalty_settings");

            migrationBuilder.DropColumn(
                name: "SessionEnabled",
                table: "organization_loyalty_settings");

            migrationBuilder.DropColumn(
                name: "SessionPercentBasisPoints",
                table: "organization_loyalty_settings");
        }
    }
}
