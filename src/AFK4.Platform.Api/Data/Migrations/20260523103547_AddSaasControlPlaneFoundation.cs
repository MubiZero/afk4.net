using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaasControlPlaneFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LimitsJson",
                table: "organizations",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "PlanCode",
                table: "organizations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "starter");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "organizations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "organizations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedAtUtc",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "organizations",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "organizations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "trial");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "organizations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "branches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Backfill deterministic, globally unique slugs for any pre-existing tenant rows
            // before the unique slug indexes are created below. Application code is responsible
            // for setting slugs on new inserts, so the DEFAULT '' is dropped after backfill.
            migrationBuilder.Sql(
                "UPDATE organizations SET \"Slug\" = 'org-' || substring(replace(\"OrganizationId\"::text, '-', ''), 1, 12), \"UpdatedAtUtc\" = \"CreatedAtUtc\" WHERE \"Slug\" = '';");
            migrationBuilder.Sql(
                "UPDATE branches SET \"Slug\" = 'branch-' || substring(replace(\"BranchId\"::text, '-', ''), 1, 12) WHERE \"Slug\" = '';");
            migrationBuilder.Sql(
                "ALTER TABLE organizations ALTER COLUMN \"Slug\" DROP DEFAULT;");
            migrationBuilder.Sql(
                "ALTER TABLE organizations ALTER COLUMN \"UpdatedAtUtc\" DROP DEFAULT;");
            migrationBuilder.Sql(
                "ALTER TABLE branches ALTER COLUMN \"Slug\" DROP DEFAULT;");

            migrationBuilder.CreateTable(
                name: "owner_invites",
                columns: table => new
                {
                    OwnerInviteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    OwnerDisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owner_invites", x => x.OwnerInviteId);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_access_tokens",
                columns: table => new
                {
                    PlatformAdminAccessTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_admin_access_tokens", x => x.PlatformAdminAccessTokenId);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_refresh_tokens",
                columns: table => new
                {
                    PlatformAdminRefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_admin_refresh_tokens", x => x.PlatformAdminRefreshTokenId);
                });

            migrationBuilder.CreateTable(
                name: "platform_admin_users",
                columns: table => new
                {
                    PlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    RolesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_admin_users", x => x.PlatformAdminUserId);
                });

            migrationBuilder.CreateTable(
                name: "tenant_support_notes",
                columns: table => new
                {
                    TenantSupportNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_support_notes", x => x.TenantSupportNoteId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                table: "organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Status",
                table: "organizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_branches_OrganizationId_Slug",
                table: "branches",
                columns: new[] { "OrganizationId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_owner_invites_ExpiresAtUtc",
                table: "owner_invites",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_owner_invites_NormalizedCode",
                table: "owner_invites",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_owner_invites_OrganizationId_BranchId_Status",
                table: "owner_invites",
                columns: new[] { "OrganizationId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_access_tokens_PlatformAdminUserId_ExpiresAtU~",
                table: "platform_admin_access_tokens",
                columns: new[] { "PlatformAdminUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_access_tokens_TokenHash",
                table: "platform_admin_access_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_refresh_tokens_PlatformAdminUserId_ExpiresAt~",
                table: "platform_admin_refresh_tokens",
                columns: new[] { "PlatformAdminUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_refresh_tokens_TokenHash",
                table: "platform_admin_refresh_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_platform_admin_users_NormalizedUserName",
                table: "platform_admin_users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_support_notes_OrganizationId_CreatedAtUtc",
                table: "tenant_support_notes",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner_invites");

            migrationBuilder.DropTable(
                name: "platform_admin_access_tokens");

            migrationBuilder.DropTable(
                name: "platform_admin_refresh_tokens");

            migrationBuilder.DropTable(
                name: "platform_admin_users");

            migrationBuilder.DropTable(
                name: "tenant_support_notes");

            migrationBuilder.DropIndex(
                name: "IX_organizations_Slug",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_organizations_Status",
                table: "organizations");

            migrationBuilder.DropIndex(
                name: "IX_branches_OrganizationId_Slug",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "LimitsJson",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "PlanCode",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "branches");
        }
    }
}
