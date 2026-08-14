using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationBillingChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EstimatedCostMinorUnits",
                table: "reservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TariffVersionId",
                table: "reservations",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "EstimatedCostMinorUnits",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "TariffVersionId",
                table: "reservations");
        }
    }
}
