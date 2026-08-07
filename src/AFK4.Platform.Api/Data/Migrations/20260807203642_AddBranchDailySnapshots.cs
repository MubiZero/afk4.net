using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchDailySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_daily_snapshots",
                columns: table => new
                {
                    BranchDailySnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SessionCount = table.Column<int>(type: "integer", nullable: false),
                    RevenueMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ShiftOpenedCount = table.Column<int>(type: "integer", nullable: false),
                    AgentAlive = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_daily_snapshots", x => x.BranchDailySnapshotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branch_daily_snapshots_Branch_Date",
                table: "branch_daily_snapshots",
                columns: new[] { "BranchId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branch_daily_snapshots_OrganizationId_SnapshotDate",
                table: "branch_daily_snapshots",
                columns: new[] { "OrganizationId", "SnapshotDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_daily_snapshots");
        }
    }
}
