using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_commands",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_commands", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "device_credentials",
                columns: table => new
                {
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_credentials", x => x.CredentialId);
                });

            migrationBuilder.CreateTable(
                name: "device_enrollment_codes",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_enrollment_codes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AgentVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShellVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EnrolledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.DeviceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_commands_DeviceId_CommandId",
                table: "device_commands",
                columns: new[] { "DeviceId", "CommandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_credentials_DeviceId",
                table: "device_credentials",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_device_credentials_OrganizationId_BranchId_DeviceId",
                table: "device_credentials",
                columns: new[] { "OrganizationId", "BranchId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_device_enrollment_codes_ExpiresAtUtc",
                table: "device_enrollment_codes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_device_enrollment_codes_OrganizationId_BranchId",
                table: "device_enrollment_codes",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_devices_OrganizationId_BranchId",
                table: "devices",
                columns: new[] { "OrganizationId", "BranchId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_commands");

            migrationBuilder.DropTable(
                name: "device_credentials");

            migrationBuilder.DropTable(
                name: "device_enrollment_codes");

            migrationBuilder.DropTable(
                name: "devices");
        }
    }
}
