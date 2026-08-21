using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingTruthfulness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NoShowAtUtc",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReasonCode",
                table: "reservations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReasonNote",
                table: "reservations",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RejectedAtUtc",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RetainedAmountMinorUnits",
                table: "reservations",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origin",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "NoShowAtUtc",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "RejectReasonCode",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "RejectReasonNote",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "RetainedAmountMinorUnits",
                table: "reservations");
        }
    }
}
