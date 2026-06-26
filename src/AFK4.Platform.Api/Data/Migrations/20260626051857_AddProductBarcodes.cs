using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBarcodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_barcodes",
                columns: table => new
                {
                    BarcodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_barcodes", x => x.BarcodeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_barcodes_OrganizationId_BranchId_Code",
                table: "product_barcodes",
                columns: new[] { "OrganizationId", "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_barcodes_OrganizationId_BranchId_ProductId",
                table: "product_barcodes",
                columns: new[] { "OrganizationId", "BranchId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_barcodes");
        }
    }
}
