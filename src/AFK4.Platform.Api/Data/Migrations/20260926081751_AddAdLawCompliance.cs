using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdLawCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "ad_creatives",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyRu",
                table: "ad_creatives",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleRu",
                table: "ad_creatives",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContainsOffer",
                table: "ad_campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DistanceSelling",
                table: "ad_campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PermitNumber",
                table: "ad_campaigns",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCertification",
                table: "ad_campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ad_advertisers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "ad_advertisers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "ad_advertisers",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ad_creative_images",
                columns: table => new
                {
                    CreativeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    StoredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ad_creative_images", x => x.CreativeId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ad_creative_images");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "ad_creatives");

            migrationBuilder.DropColumn(
                name: "BodyRu",
                table: "ad_creatives");

            migrationBuilder.DropColumn(
                name: "TitleRu",
                table: "ad_creatives");

            migrationBuilder.DropColumn(
                name: "ContainsOffer",
                table: "ad_campaigns");

            migrationBuilder.DropColumn(
                name: "DistanceSelling",
                table: "ad_campaigns");

            migrationBuilder.DropColumn(
                name: "PermitNumber",
                table: "ad_campaigns");

            migrationBuilder.DropColumn(
                name: "RequiresCertification",
                table: "ad_campaigns");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "ad_advertisers");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "ad_advertisers");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "ad_advertisers");
        }
    }
}
