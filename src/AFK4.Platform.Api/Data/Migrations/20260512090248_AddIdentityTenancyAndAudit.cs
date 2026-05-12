using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityTenancyAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    AuditRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorStaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceApp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.AuditRecordId);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.BranchId);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.OrganizationId);
                });

            migrationBuilder.CreateTable(
                name: "staff_access_tokens",
                columns: table => new
                {
                    StaffAccessTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_access_tokens", x => x.StaffAccessTokenId);
                });

            migrationBuilder.CreateTable(
                name: "staff_role_assignments",
                columns: table => new
                {
                    StaffRoleAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_role_assignments", x => x.StaffRoleAssignmentId);
                });

            migrationBuilder.CreateTable(
                name: "staff_users",
                columns: table => new
                {
                    StaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_users", x => x.StaffUserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_OrganizationId_BranchId_CreatedAtUtc",
                table: "audit_records",
                columns: new[] { "OrganizationId", "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_branches_OrganizationId_BranchId",
                table: "branches",
                columns: new[] { "OrganizationId", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_tokens_StaffUserId_ExpiresAtUtc",
                table: "staff_access_tokens",
                columns: new[] { "StaffUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_access_tokens_TokenHash",
                table: "staff_access_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_staff_role_assignments_StaffUserId_OrganizationId_BranchId_~",
                table: "staff_role_assignments",
                columns: new[] { "StaffUserId", "OrganizationId", "BranchId", "RoleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_users_OrganizationId_NormalizedUserName",
                table: "staff_users",
                columns: new[] { "OrganizationId", "NormalizedUserName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "staff_access_tokens");

            migrationBuilder.DropTable(
                name: "staff_role_assignments");

            migrationBuilder.DropTable(
                name: "staff_users");
        }
    }
}
