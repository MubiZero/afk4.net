using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkPaymentsToLedgerEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LedgerEntryId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_LedgerEntryId",
                table: "payments",
                column: "LedgerEntryId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_ledger_entries_LedgerEntryId",
                table: "payments",
                column: "LedgerEntryId",
                principalTable: "ledger_entries",
                principalColumn: "LedgerEntryId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_ledger_entries_LedgerEntryId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_LedgerEntryId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "LedgerEntryId",
                table: "payments");
        }
    }
}
