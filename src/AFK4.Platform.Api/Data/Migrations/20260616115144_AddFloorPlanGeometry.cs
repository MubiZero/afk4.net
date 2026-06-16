using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorPlanGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "zones",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeoHeight",
                table: "zones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeoWidth",
                table: "zones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeoX",
                table: "zones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeoY",
                table: "zones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZoneType",
                table: "zones",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PosX",
                table: "seats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PosY",
                table: "seats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rotation",
                table: "seats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SeatType",
                table: "seats",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "pc");

            migrationBuilder.CreateTable(
                name: "walls",
                columns: table => new
                {
                    WallId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    X1 = table.Column<int>(type: "integer", nullable: false),
                    Y1 = table.Column<int>(type: "integer", nullable: false),
                    X2 = table.Column<int>(type: "integer", nullable: false),
                    Y2 = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_walls", x => x.WallId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_walls_OrganizationId_BranchId",
                table: "walls",
                columns: new[] { "OrganizationId", "BranchId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "walls");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "GeoHeight",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "GeoWidth",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "GeoX",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "GeoY",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "ZoneType",
                table: "zones");

            migrationBuilder.DropColumn(
                name: "PosX",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "PosY",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "seats");

            migrationBuilder.DropColumn(
                name: "SeatType",
                table: "seats");
        }
    }
}
