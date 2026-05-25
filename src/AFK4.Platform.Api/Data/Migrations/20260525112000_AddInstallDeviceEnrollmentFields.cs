using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallDeviceEnrollmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "devices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "EnrolledViaOwnerCodeId",
                table: "devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnrollmentState",
                table: "devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "approved");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "gaming_pc");

            migrationBuilder.Sql("""
                UPDATE devices
                SET "DisplayName" = "MachineName"
                WHERE "DisplayName" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_devices_EnrolledViaOwnerCodeId",
                table: "devices",
                column: "EnrolledViaOwnerCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_devices_OrganizationId_BranchId_EnrollmentState",
                table: "devices",
                columns: new[] { "OrganizationId", "BranchId", "EnrollmentState" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_devices_EnrolledViaOwnerCodeId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_OrganizationId_BranchId_EnrollmentState",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "EnrolledViaOwnerCodeId",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "EnrollmentState",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "devices");
        }
    }
}
