using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShopOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvailableInShell",
                table: "pos_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "shop_order_lines",
                columns: table => new
                {
                    ShopOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UnitPriceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    LineTotalMinorUnits = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_order_lines", x => x.ShopOrderLineId);
                });

            migrationBuilder.CreateTable(
                name: "shop_orders",
                columns: table => new
                {
                    ShopOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TotalMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    WalletLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_orders", x => x.ShopOrderId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shop_order_lines_ShopOrderId",
                table: "shop_order_lines",
                column: "ShopOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_orders_BranchId_Status",
                table: "shop_orders",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_orders_PlayerAccountId_PlacedAtUtc",
                table: "shop_orders",
                columns: new[] { "PlayerAccountId", "PlacedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_order_lines");

            migrationBuilder.DropTable(
                name: "shop_orders");

            migrationBuilder.DropColumn(
                name: "AvailableInShell",
                table: "pos_products");
        }
    }
}
