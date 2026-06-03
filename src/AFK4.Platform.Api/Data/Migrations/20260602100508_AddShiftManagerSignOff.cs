using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftManagerSignOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagerSignOffStaffUserId",
                table: "shifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignOffReason",
                table: "shifts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagerSignOffStaffUserId",
                table: "shifts");

            migrationBuilder.DropColumn(
                name: "SignOffReason",
                table: "shifts");
        }
    }
}
