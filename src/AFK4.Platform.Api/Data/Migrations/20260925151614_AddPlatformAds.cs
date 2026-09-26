using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ad_advertisers",
                columns: table => new
                {
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Contact = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_advertisers", x => x.AdvertiserId);
                });

            migrationBuilder.CreateTable(
                name: "ad_campaigns",
                columns: table => new
                {
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvertiserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CitiesJson = table.Column<string>(type: "text", nullable: false),
                    OrganizationIdsJson = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_campaigns", x => x.CampaignId);
                });

            migrationBuilder.CreateTable(
                name: "ad_creatives",
                columns: table => new
                {
                    CreativeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Moderation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RejectedReason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ModeratedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModeratedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_creatives", x => x.CreativeId);
                });

            migrationBuilder.CreateTable(
                name: "ad_impression_batches",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_impression_batches", x => new { x.DeviceId, x.BatchId });
                });

            migrationBuilder.CreateTable(
                name: "ad_impressions_daily",
                columns: table => new
                {
                    AdImpressionDailyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreativeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    ShownMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_impressions_daily", x => x.AdImpressionDailyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_AdvertiserId",
                table: "ad_campaigns",
                column: "AdvertiserId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_campaigns_State_EndsAtUtc",
                table: "ad_campaigns",
                columns: new[] { "State", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ad_creatives_CampaignId",
                table: "ad_creatives",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_impression_batches_OrganizationId",
                table: "ad_impression_batches",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ad_impressions_daily_CreativeId_BranchId_Day",
                table: "ad_impressions_daily",
                columns: new[] { "CreativeId", "BranchId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ad_impressions_daily_Day",
                table: "ad_impressions_daily",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_ad_impressions_daily_OrganizationId_Day",
                table: "ad_impressions_daily",
                columns: new[] { "OrganizationId", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_advertisers");

            migrationBuilder.DropTable(
                name: "ad_campaigns");

            migrationBuilder.DropTable(
                name: "ad_creatives");

            migrationBuilder.DropTable(
                name: "ad_impression_batches");

            migrationBuilder.DropTable(
                name: "ad_impressions_daily");
        }
    }
}
