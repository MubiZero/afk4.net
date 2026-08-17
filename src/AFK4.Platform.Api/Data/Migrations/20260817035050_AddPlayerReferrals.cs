using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "player_accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organization_referral_settings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReferrerBonusMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    InviteeBonusMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    MinimumTopUpMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    ClaimWindowDays = table.Column<int>(type: "integer", nullable: false),
                    MaxRewardedPerReferrer = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_referral_settings", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "player_referrals",
                columns: table => new
                {
                    InviteePlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerPlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RewardedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReferrerBonusMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    InviteeBonusMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_referrals", x => x.InviteePlayerAccountId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_accounts_OrganizationId_ReferralCode",
                table: "player_accounts",
                columns: new[] { "OrganizationId", "ReferralCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_referrals_OrganizationId_ReferrerPlayerAccountId",
                table: "player_referrals",
                columns: new[] { "OrganizationId", "ReferrerPlayerAccountId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_referral_settings");

            migrationBuilder.DropTable(
                name: "player_referrals");

            migrationBuilder.DropIndex(
                name: "IX_player_accounts_OrganizationId_ReferralCode",
                table: "player_accounts");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "player_accounts");
        }
    }
}
