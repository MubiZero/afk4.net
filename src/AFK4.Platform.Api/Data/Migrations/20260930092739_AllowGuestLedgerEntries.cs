using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowGuestLedgerEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerAccountId",
                table: "ledger_entries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Записи гостя без аккаунта прежняя схема хранить не умеет: оставить их с нулевым игроком
            // значило бы выдать деньги гостя за движение по чужому счёту.
            migrationBuilder.Sql("DELETE FROM ledger_entries WHERE \"PlayerAccountId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "PlayerAccountId",
                table: "ledger_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
