using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationUpdateChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinnedClientVersion",
                table: "organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdateChannel",
                table: "organizations",
                type: "text",
                nullable: false,
                defaultValue: "stable");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinnedClientVersion",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "UpdateChannel",
                table: "organizations");
        }
    }
}
