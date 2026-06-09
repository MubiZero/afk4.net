using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionVersionConcurrencyGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_seat_active_unique",
                table: "sessions",
                column: "SeatId",
                unique: true,
                filter: "\"State\" IN ('active', 'paused', 'ending')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sessions_seat_active_unique",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "sessions");
        }
    }
}
