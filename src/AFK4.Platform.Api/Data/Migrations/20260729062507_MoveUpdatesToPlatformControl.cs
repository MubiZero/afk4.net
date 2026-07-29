using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveUpdatesToPlatformControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows belong to the removed organization-managed release model and cannot be
            // attributed to a platform administrator truthfully. The pre-launch big-bang cutover
            // deliberately starts the release catalog, rollout history, and device status history clean.
            migrationBuilder.Sql("DELETE FROM device_update_statuses;");
            migrationBuilder.Sql("DELETE FROM update_rollout_targets;");
            migrationBuilder.Sql("DELETE FROM update_rollouts;");
            migrationBuilder.Sql("DELETE FROM update_packages;");

            migrationBuilder.DropIndex(
                name: "IX_update_rollouts_OrganizationId_BranchId_Channel_State_Start~",
                table: "update_rollouts");

            migrationBuilder.DropIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_DeviceId",
                table: "update_rollout_targets");

            migrationBuilder.DropIndex(
                name: "IX_update_packages_OrganizationId_BranchId_Component_Version_C~",
                table: "update_packages");

            migrationBuilder.DropIndex(
                name: "IX_update_packages_OrganizationId_BranchId_CreatedAtUtc",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "update_rollouts");

            migrationBuilder.DropColumn(
                name: "CreatedByStaffUserId",
                table: "update_rollouts");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "CreatedByStaffUserId",
                table: "update_packages");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "update_rollouts",
                newName: "CreatedByPlatformAdminUserId");

            migrationBuilder.RenameColumn(
                name: "OrganizationId",
                table: "update_packages",
                newName: "CreatedByPlatformAdminUserId");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "update_rollout_targets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "update_rollout_targets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAtUtc",
                table: "update_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidatedAtUtc",
                table: "update_packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidatedByPlatformAdminUserId",
                table: "update_packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_update_rollouts_Channel_State_StartsAtUtc",
                table: "update_rollouts",
                columns: new[] { "Channel", "State", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_BranchId",
                table: "update_rollout_targets",
                columns: new[] { "UpdateRolloutId", "BranchId" },
                unique: true,
                filter: "\"BranchId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_DeviceId",
                table: "update_rollout_targets",
                columns: new[] { "UpdateRolloutId", "DeviceId" },
                unique: true,
                filter: "\"DeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_OrganizationId",
                table: "update_rollout_targets",
                columns: new[] { "UpdateRolloutId", "OrganizationId" },
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_update_packages_Component_Version_Channel",
                table: "update_packages",
                columns: new[] { "Component", "Version", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_update_packages_State_CreatedAtUtc",
                table: "update_packages",
                columns: new[] { "State", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_update_rollouts_Channel_State_StartsAtUtc",
                table: "update_rollouts");

            migrationBuilder.DropIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_BranchId",
                table: "update_rollout_targets");

            migrationBuilder.DropIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_DeviceId",
                table: "update_rollout_targets");

            migrationBuilder.DropIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_OrganizationId",
                table: "update_rollout_targets");

            migrationBuilder.DropIndex(
                name: "IX_update_packages_Component_Version_Channel",
                table: "update_packages");

            migrationBuilder.DropIndex(
                name: "IX_update_packages_State_CreatedAtUtc",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "RetiredAtUtc",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "ValidatedAtUtc",
                table: "update_packages");

            migrationBuilder.DropColumn(
                name: "ValidatedByPlatformAdminUserId",
                table: "update_packages");

            migrationBuilder.RenameColumn(
                name: "CreatedByPlatformAdminUserId",
                table: "update_rollouts",
                newName: "OrganizationId");

            migrationBuilder.RenameColumn(
                name: "CreatedByPlatformAdminUserId",
                table: "update_packages",
                newName: "OrganizationId");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "update_rollouts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByStaffUserId",
                table: "update_rollouts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                table: "update_rollout_targets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "BranchId",
                table: "update_rollout_targets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "update_packages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByStaffUserId",
                table: "update_packages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_update_rollouts_OrganizationId_BranchId_Channel_State_Start~",
                table: "update_rollouts",
                columns: new[] { "OrganizationId", "BranchId", "Channel", "State", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_update_rollout_targets_UpdateRolloutId_DeviceId",
                table: "update_rollout_targets",
                columns: new[] { "UpdateRolloutId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_update_packages_OrganizationId_BranchId_Component_Version_C~",
                table: "update_packages",
                columns: new[] { "OrganizationId", "BranchId", "Component", "Version", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_update_packages_OrganizationId_BranchId_CreatedAtUtc",
                table: "update_packages",
                columns: new[] { "OrganizationId", "BranchId", "CreatedAtUtc" });
        }
    }
}
