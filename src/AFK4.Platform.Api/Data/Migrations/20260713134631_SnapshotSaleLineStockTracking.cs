using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotSaleLineStockTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TrackStock",
                table: "pos_sale_lines",
                newName: "TracksStock");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TracksStock",
                table: "pos_sale_lines",
                newName: "TrackStock");
        }
    }
}
