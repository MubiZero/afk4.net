using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    NotificationPreferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OptedOut = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.NotificationPreferenceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_PlayerAccountId_Category_Channel",
                table: "notification_preferences",
                columns: new[] { "PlayerAccountId", "Category", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_StaffUserId_Category_Channel",
                table: "notification_preferences",
                columns: new[] { "StaffUserId", "Category", "Channel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_preferences");
        }
    }
}
