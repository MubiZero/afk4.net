using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformIncidentsAndJobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_incidents",
                columns: table => new
                {
                    PlatformIncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DedupKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DetailsJson = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastNotifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_incidents", x => x.PlatformIncidentId);
                });

            migrationBuilder.CreateTable(
                name: "platform_job_runs",
                columns: table => new
                {
                    PlatformJobRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ItemsProcessed = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_job_runs", x => x.PlatformJobRunId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_incidents_DedupKey",
                table: "platform_incidents",
                column: "DedupKey",
                unique: true,
                filter: "\"ResolvedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_platform_incidents_OpenedAtUtc",
                table: "platform_incidents",
                column: "OpenedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_platform_job_runs_JobName_StartedAtUtc",
                table: "platform_job_runs",
                columns: new[] { "JobName", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_incidents");

            migrationBuilder.DropTable(
                name: "platform_job_runs");
        }
    }
}
