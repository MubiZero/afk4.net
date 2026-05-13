using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPosInventoryShiftsReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "ledger_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_movements",
                columns: table => new
                {
                    CashMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_movements", x => x.CashMovementId);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.PaymentId);
                });

            migrationBuilder.CreateTable(
                name: "pos_product_categories",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_product_categories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "pos_products",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Sku = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PriceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    TrackStock = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNegativeStock = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_products", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "pos_sale_lines",
                columns: table => new
                {
                    PosSaleLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UnitPriceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    LineTotalMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    TrackStock = table.Column<bool>(type: "boolean", nullable: false),
                    AllowNegativeStock = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_sale_lines", x => x.PosSaleLineId);
                });

            migrationBuilder.CreateTable(
                name: "pos_sales",
                columns: table => new
                {
                    PosSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TotalMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    RefundReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    VoidReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefundedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_sales", x => x.PosSaleId);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PosSaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceiptType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TotalMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.ReceiptId);
                });

            migrationBuilder.CreateTable(
                name: "shifts",
                columns: table => new
                {
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    StartingCashMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CountedCashMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedCashMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    DifferenceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    OpeningNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ClosingNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shifts", x => x.ShiftId);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuantityDelta = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    UnitCostMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.StockMovementId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_ShiftId_CreatedAtUtc",
                table: "ledger_entries",
                columns: new[] { "ShiftId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_ShiftId_CreatedAtUtc",
                table: "cash_movements",
                columns: new[] { "ShiftId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_PosSaleId_CreatedAtUtc",
                table: "payments",
                columns: new[] { "PosSaleId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_product_categories_OrganizationId_BranchId_Name",
                table: "pos_product_categories",
                columns: new[] { "OrganizationId", "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_products_OrganizationId_BranchId_CategoryId",
                table: "pos_products",
                columns: new[] { "OrganizationId", "BranchId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_products_OrganizationId_BranchId_Sku",
                table: "pos_products",
                columns: new[] { "OrganizationId", "BranchId", "Sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_sale_lines_PosSaleId",
                table: "pos_sale_lines",
                column: "PosSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_pos_sales_OrganizationId_BranchId_ShiftId_CreatedAtUtc",
                table: "pos_sales",
                columns: new[] { "OrganizationId", "BranchId", "ShiftId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_sales_State",
                table: "pos_sales",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_OrganizationId_BranchId_ReceiptNumber",
                table: "receipts",
                columns: new[] { "OrganizationId", "BranchId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_PosSaleId",
                table: "receipts",
                column: "PosSaleId");

            migrationBuilder.CreateIndex(
                name: "IX_shifts_OrganizationId_BranchId_State",
                table: "shifts",
                columns: new[] { "OrganizationId", "BranchId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_OrganizationId_BranchId_CreatedAtUtc",
                table: "stock_movements",
                columns: new[] { "OrganizationId", "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ProductId_CreatedAtUtc",
                table: "stock_movements",
                columns: new[] { "ProductId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_movements");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "pos_product_categories");

            migrationBuilder.DropTable(
                name: "pos_products");

            migrationBuilder.DropTable(
                name: "pos_sale_lines");

            migrationBuilder.DropTable(
                name: "pos_sales");

            migrationBuilder.DropTable(
                name: "receipts");

            migrationBuilder.DropTable(
                name: "shifts");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_ShiftId_CreatedAtUtc",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "ledger_entries");
        }
    }
}
