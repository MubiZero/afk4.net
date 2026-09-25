using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShowcaseFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FeaturedOnPcs",
                table: "tariffs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FeaturedOnPcs",
                table: "pos_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "pos_products",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnPcs",
                table: "news_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeaturedOnPcs",
                table: "tariffs");

            migrationBuilder.DropColumn(
                name: "FeaturedOnPcs",
                table: "pos_products");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "pos_products");

            migrationBuilder.DropColumn(
                name: "ShowOnPcs",
                table: "news_items");
        }
    }
}
