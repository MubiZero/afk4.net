using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitTips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_tip_settings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_tip_settings", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "shift_tip_payouts",
                columns: table => new
                {
                    ShiftTipPayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_tip_payouts", x => x.ShiftTipPayoutId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shift_tip_payouts_CashMovementId",
                table: "shift_tip_payouts",
                column: "CashMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shift_tip_payouts_OrganizationId",
                table: "shift_tip_payouts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_tip_payouts_ShiftId",
                table: "shift_tip_payouts",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_tip_settings");

            migrationBuilder.DropTable(
                name: "shift_tip_payouts");
        }
    }
}
