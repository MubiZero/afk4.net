using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineLeaseKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NetworkAddress",
                table: "devices",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "offline_lease_keys",
                columns: table => new
                {
                    OfflineLeaseKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicKeyPem = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RegisteredByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offline_lease_keys", x => x.OfflineLeaseKeyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_offline_lease_keys_BranchId_KeyId",
                table: "offline_lease_keys",
                columns: new[] { "BranchId", "KeyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offline_lease_keys");

            migrationBuilder.DropColumn(
                name: "NetworkAddress",
                table: "devices");
        }
    }
}
