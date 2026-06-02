using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionCheckoutLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PosSaleId",
                table: "receipts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "pos_sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PosSaleId",
                table: "payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipts_SessionId",
                table: "receipts",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_pos_sales_SessionId",
                table: "pos_sales",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_SessionId_CreatedAtUtc",
                table: "payments",
                columns: new[] { "SessionId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_receipts_SessionId",
                table: "receipts");

            migrationBuilder.DropIndex(
                name: "IX_pos_sales_SessionId",
                table: "pos_sales");

            migrationBuilder.DropIndex(
                name: "IX_payments_SessionId_CreatedAtUtc",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "pos_sales");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "PosSaleId",
                table: "receipts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PosSaleId",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
