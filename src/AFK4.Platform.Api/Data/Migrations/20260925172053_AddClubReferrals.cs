using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClubReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeMonths",
                table: "tenant_subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "organizations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReferralRewardedAtUtc",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByOrganizationId",
                table: "organizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_ReferralCode",
                table: "organizations",
                column: "ReferralCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organizations_ReferralCode",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "FreeMonths",
                table: "tenant_subscriptions");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ReferralRewardedAtUtc",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "ReferredByOrganizationId",
                table: "organizations");
        }
    }
}
