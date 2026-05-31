using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaasSubscriptionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.InvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                columns: table => new
                {
                    PlanCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PriceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BillingInterval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MaxBranches = table.Column<int>(type: "integer", nullable: true),
                    MaxDevicesPerBranch = table.Column<int>(type: "integer", nullable: true),
                    MaxConcurrentSessions = table.Column<int>(type: "integer", nullable: true),
                    MaxStaffUsersPerBranch = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.PlanCode);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscriptions",
                columns: table => new
                {
                    TenantSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentPeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextInvoiceUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BillingInterval = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CancelAtPeriodEnd = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscriptions", x => x.TenantSubscriptionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Number",
                table: "invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_OrganizationId_IssuedAtUtc",
                table: "invoices",
                columns: new[] { "OrganizationId", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status_DueAtUtc",
                table: "invoices",
                columns: new[] { "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_SortOrder",
                table: "subscription_plans",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscriptions_OrganizationId",
                table: "tenant_subscriptions",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_subscriptions_Status_NextInvoiceUtc",
                table: "tenant_subscriptions",
                columns: new[] { "Status", "NextInvoiceUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "subscription_plans");

            migrationBuilder.DropTable(
                name: "tenant_subscriptions");
        }
    }
}
