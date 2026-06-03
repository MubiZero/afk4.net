using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffMoneyCaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "staff_money_caps",
                columns: table => new
                {
                    StaffMoneyCapId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActionScope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PerTransactionMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    DailyMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_money_caps", x => x.StaffMoneyCapId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_money_caps_BranchId_RoleName_ActionScope",
                table: "staff_money_caps",
                columns: new[] { "BranchId", "RoleName", "ActionScope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_money_caps");
        }
    }
}
