using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscountAmountMinorUnits",
                table: "tenant_subscriptions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountPercent",
                table: "tenant_subscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "tenant_subscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscountUntilUtc",
                table: "tenant_subscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmountMinorUnits",
                table: "tenant_subscriptions");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "tenant_subscriptions");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "tenant_subscriptions");

            migrationBuilder.DropColumn(
                name: "DiscountUntilUtc",
                table: "tenant_subscriptions");
        }
    }
}
