using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPosSalePlayerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PlayerAccountId",
                table: "pos_sales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pos_sales_PlayerAccountId",
                table: "pos_sales",
                column: "PlayerAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pos_sales_PlayerAccountId",
                table: "pos_sales");

            migrationBuilder.DropColumn(
                name: "PlayerAccountId",
                table: "pos_sales");
        }
    }
}
