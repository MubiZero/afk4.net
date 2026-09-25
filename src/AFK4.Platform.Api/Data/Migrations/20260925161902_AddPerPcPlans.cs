using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerPcPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PromisedPaymentInvoiceId",
                table: "tenant_subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialStartedAtUtc",
                table: "tenant_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncludedDevices",
                table: "subscription_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "PricePerDeviceMinorUnits",
                table: "subscription_plans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Прежняя сетка (Starter за 2 900 — рубли без пересчёта) снимается с продажи: новых клубов на
            // неё не ставят, клубы на ней остаются. Платформа может вернуть тариф в продажу из панели.
            migrationBuilder.Sql("""
                UPDATE subscription_plans SET "IsActive" = false
                WHERE "PlanCode" IN ('starter', 'growth', 'scale', 'starter_yearly', 'growth_yearly', 'scale_yearly');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromisedPaymentInvoiceId",
                table: "tenant_subscriptions");

            migrationBuilder.DropColumn(
                name: "TrialStartedAtUtc",
                table: "tenant_subscriptions");

            migrationBuilder.DropColumn(
                name: "IncludedDevices",
                table: "subscription_plans");

            migrationBuilder.DropColumn(
                name: "PricePerDeviceMinorUnits",
                table: "subscription_plans");
        }
    }
}
