using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDunningTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscountMinorUnits",
                table: "invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueSoonNotifiedAtUtc",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DunningStage",
                table: "invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "GrossAmountMinorUnits",
                table: "invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDunningAtUtc",
                table: "invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""UPDATE "invoices" SET "GrossAmountMinorUnits" = "AmountMinorUnits";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountMinorUnits",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "DueSoonNotifiedAtUtc",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "DunningStage",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "GrossAmountMinorUnits",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "LastDunningAtUtc",
                table: "invoices");
        }
    }
}
