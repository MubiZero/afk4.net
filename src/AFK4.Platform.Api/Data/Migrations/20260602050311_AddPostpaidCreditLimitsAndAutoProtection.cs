using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPostpaidCreditLimitsAndAutoProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AutoLockedAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AutoWarnedAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PostpaidCreditLimitMinorUnits",
                table: "player_accounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PostpaidCreditLimitMinorUnits",
                table: "branches",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoLockedAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "AutoWarnedAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "PostpaidCreditLimitMinorUnits",
                table: "player_accounts");

            migrationBuilder.DropColumn(
                name: "PostpaidCreditLimitMinorUnits",
                table: "branches");
        }
    }
}
