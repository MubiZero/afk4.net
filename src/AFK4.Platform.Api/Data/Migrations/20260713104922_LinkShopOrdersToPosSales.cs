using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkShopOrdersToPosSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PosSaleId",
                table: "shop_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UnitCostMinorUnits",
                table: "pos_sale_lines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_shop_orders_PosSaleId",
                table: "shop_orders",
                column: "PosSaleId",
                unique: true,
                filter: "\"PosSaleId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_shop_orders_pos_sales_PosSaleId",
                table: "shop_orders",
                column: "PosSaleId",
                principalTable: "pos_sales",
                principalColumn: "PosSaleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shop_orders_pos_sales_PosSaleId",
                table: "shop_orders");

            migrationBuilder.DropIndex(
                name: "IX_shop_orders_PosSaleId",
                table: "shop_orders");

            migrationBuilder.DropColumn(
                name: "PosSaleId",
                table: "shop_orders");

            migrationBuilder.DropColumn(
                name: "UnitCostMinorUnits",
                table: "pos_sale_lines");
        }
    }
}
