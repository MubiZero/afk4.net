using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDailySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscription_daily_snapshots",
                columns: table => new
                {
                    SubscriptionDailySnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MonthlyAmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_daily_snapshots", x => x.SubscriptionDailySnapshotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_daily_snapshots_Organization_Date",
                table: "subscription_daily_snapshots",
                columns: new[] { "OrganizationId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_daily_snapshots_SnapshotDate",
                table: "subscription_daily_snapshots",
                column: "SnapshotDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_daily_snapshots");
        }
    }
}
