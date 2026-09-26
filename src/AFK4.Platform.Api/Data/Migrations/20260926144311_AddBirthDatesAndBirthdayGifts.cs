using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthDatesAndBirthdayGifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "platform_persons",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BirthDateSetAtUtc",
                table: "platform_persons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organization_birthday_gift_settings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    RecentVisitDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_birthday_gift_settings", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "player_birthday_gifts",
                columns: table => new
                {
                    PlayerBirthdayGiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_birthday_gifts", x => x.PlayerBirthdayGiftId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_birthday_gifts_OrganizationId",
                table: "player_birthday_gifts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_player_birthday_gifts_PlayerAccountId_Year",
                table: "player_birthday_gifts",
                columns: new[] { "PlayerAccountId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_birthday_gift_settings");

            migrationBuilder.DropTable(
                name: "player_birthday_gifts");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "platform_persons");

            migrationBuilder.DropColumn(
                name: "BirthDateSetAtUtc",
                table: "platform_persons");
        }
    }
}
