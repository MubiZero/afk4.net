using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchMoneyControlSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompApprovalThresholdMinorUnits",
                table: "branches",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HighRiskApprovalThresholdMinorUnits",
                table: "branches",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RefundReasonWhitelistEnabled",
                table: "branches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RefundReasonWhitelistJson",
                table: "branches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ShiftDiscrepancyToleranceMinorUnits",
                table: "branches",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompApprovalThresholdMinorUnits",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "HighRiskApprovalThresholdMinorUnits",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "RefundReasonWhitelistEnabled",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "RefundReasonWhitelistJson",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "ShiftDiscrepancyToleranceMinorUnits",
                table: "branches");
        }
    }
}
