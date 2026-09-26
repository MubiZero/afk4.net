using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LimitFreePlanByPcs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxDevices",
                table: "subscription_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KeptOnFreePlan",
                table: "devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Бесплатный тариф — только «до десяти ПК на весь клуб» (владелец, 2026-09-26). Клубы с
            // прежними лимитами бесплатного тарифа получают новые; лимиты, которые платформа задала
            // клубу сама, не трогаются.
            migrationBuilder.Sql("""
                UPDATE subscription_plans
                SET "MaxBranches" = NULL, "MaxDevicesPerBranch" = NULL, "MaxStaffUsersPerBranch" = NULL, "MaxDevices" = 10
                WHERE "PlanCode" = 'free';

                UPDATE organizations
                SET "LimitsJson" = '{"MaxBranches":null,"MaxDevicesPerBranch":null,"MaxConcurrentSessions":null,"MaxStaffUsersPerBranch":null,"MaxDevices":10}'::jsonb
                WHERE "PlanCode" = 'free'
                  AND ("LimitsJson" ->> 'MaxBranches') = '1'
                  AND ("LimitsJson" ->> 'MaxDevicesPerBranch') = '10'
                  AND ("LimitsJson" ->> 'MaxStaffUsersPerBranch') = '3';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxDevices",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "KeptOnFreePlan",
                table: "devices");
        }
    }
}
