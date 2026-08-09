using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_role_permissions",
                columns: table => new
                {
                    PlatformRolePermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PermissionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_role_permissions", x => x.PlatformRolePermissionId);
                });

            migrationBuilder.CreateTable(
                name: "platform_roles",
                columns: table => new
                {
                    RoleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    GrantsAllPermissions = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_roles", x => x.RoleName);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_role_permissions_Role_Permission",
                table: "platform_role_permissions",
                columns: new[] { "RoleName", "PermissionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_role_permissions_RoleName",
                table: "platform_role_permissions",
                column: "RoleName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_role_permissions");

            migrationBuilder.DropTable(
                name: "platform_roles");
        }
    }
}
