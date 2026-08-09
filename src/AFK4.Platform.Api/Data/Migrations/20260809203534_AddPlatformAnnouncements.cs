using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcement_reads",
                columns: table => new
                {
                    PlatformAnnouncementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement_reads", x => new { x.PlatformAnnouncementId, x.StaffUserId });
                });

            migrationBuilder.CreateTable(
                name: "platform_announcements",
                columns: table => new
                {
                    PlatformAnnouncementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ShowFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ShowUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AudienceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AudiencePlanCodesJson = table.Column<string>(type: "text", nullable: false),
                    AudienceOrganizationIdsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmailDispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_announcements", x => x.PlatformAnnouncementId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcement_reads_StaffUserId",
                table: "announcement_reads",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_announcements_Status_ShowUntilUtc",
                table: "platform_announcements",
                columns: new[] { "Status", "ShowUntilUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_reads");

            migrationBuilder.DropTable(
                name: "platform_announcements");
        }
    }
}
