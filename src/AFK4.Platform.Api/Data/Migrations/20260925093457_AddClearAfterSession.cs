using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClearAfterSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClearAfterSessionJson",
                table: "branch_protection_profiles",
                type: "text",
                nullable: false,
                defaultValue: "[\"steam\",\"browsers\",\"launchers\",\"messengers\"]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClearAfterSessionJson",
                table: "branch_protection_profiles");
        }
    }
}
