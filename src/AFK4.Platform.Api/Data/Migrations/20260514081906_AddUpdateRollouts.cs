using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdateRollouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_update_statuses",
                columns: table => new
                {
                    DeviceUpdateStatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateRolloutId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Component = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InstalledVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FirstReportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_update_statuses", x => x.DeviceUpdateStatusId);
                });

            migrationBuilder.CreateTable(
                name: "update_packages",
                columns: table => new
                {
                    UpdatePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Component = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ArtifactUri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Signature = table.Column<string>(type: "text", nullable: false),
                    SignatureAlgorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReleaseNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_update_packages", x => x.UpdatePackageId);
                });

            migrationBuilder.CreateTable(
                name: "update_rollout_targets",
                columns: table => new
                {
                    UpdateRolloutTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateRolloutId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_update_rollout_targets", x => x.UpdateRolloutTargetId);
                });

            migrationBuilder.CreateTable(
                name: "update_rollouts",
                columns: table => new
                {
                    UpdateRolloutId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatePackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Component = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BatchPercent = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_update_rollouts", x => x.UpdateRolloutId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_update_statuses_DeviceId_UpdateRolloutId_UpdatePacka~",
                table: "device_update_statuses",
                columns: new[] { "DeviceId", "UpdateRolloutId", "UpdatePackageId", "Component" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_update_statuses_OrganizationId_BranchId_Status_Updat~",
                table: "device_update_statuses",
                columns: new[] { "OrganizationId", "BranchId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_update_packages_OrganizationId_BranchId_Component_Version_C~",
                table: "update_packages",
                columns: new[] { "OrganizationId", "BranchId", "Component", "Version", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_update_packages_OrganizationId_BranchId_CreatedAtUtc",
                table: "update_packages",
                columns: new[] { "OrganizationId", "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_update_rollout_targets_OrganizationId_BranchId_DeviceId",
                table: "update_rollout_targets",
                columns: new[] { "OrganizationId", "BranchId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_DeviceId",
                table: "update_rollout_targets",
                columns: new[] { "UpdateRolloutId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_update_rollouts_OrganizationId_BranchId_Channel_State_Start~",
                table: "update_rollouts",
                columns: new[] { "OrganizationId", "BranchId", "Channel", "State", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_update_rollouts_UpdatePackageId",
                table: "update_rollouts",
                column: "UpdatePackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_update_statuses");

            migrationBuilder.DropTable(
                name: "update_packages");

            migrationBuilder.DropTable(
                name: "update_rollout_targets");

            migrationBuilder.DropTable(
                name: "update_rollouts");
        }
    }
}
