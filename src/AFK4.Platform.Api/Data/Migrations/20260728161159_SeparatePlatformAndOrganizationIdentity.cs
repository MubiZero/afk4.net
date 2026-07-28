using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePlatformAndOrganizationIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_support_access_grants",
                columns: table => new
                {
                    GrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_support_access_grants", x => x.GrantId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_support_access_grants_OrganizationId_ExpiresAtUtc",
                table: "platform_support_access_grants",
                columns: new[] { "OrganizationId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_support_access_grants_PlatformAdminUserId_ExpiresA~",
                table: "platform_support_access_grants",
                columns: new[] { "PlatformAdminUserId", "ExpiresAtUtc" });

            migrationBuilder.Sql(
                """
                DO $cutover$
                DECLARE unknown_roles text;
                BEGIN
                    SELECT string_agg(DISTINCT role_name, ', ' ORDER BY role_name)
                    INTO unknown_roles
                    FROM (
                        SELECT "RoleName" AS role_name FROM staff_role_assignments
                        UNION ALL
                        SELECT "RoleName" FROM staff_money_caps
                        UNION ALL
                        SELECT btrim(role_name)
                        FROM staff_invites,
                             LATERAL regexp_split_to_table("RoleNamesCsv", ',') AS role_name
                    ) roles
                    WHERE role_name NOT IN (
                        'owner', 'organization_owner', 'branch_manager', 'shift_supervisor',
                        'cashier_operator', 'operator', 'technician',
                        'accountant_auditor', 'accountant'
                    );

                    IF unknown_roles IS NOT NULL THEN
                        RAISE EXCEPTION 'Unknown organization role(s): %', unknown_roles;
                    END IF;

                    SELECT string_agg(DISTINCT role_name, ', ' ORDER BY role_name)
                    INTO unknown_roles
                    FROM platform_admin_users,
                         LATERAL jsonb_array_elements_text("RolesJson") AS role_name
                    WHERE role_name NOT IN ('platform_owner', 'platform_admin', 'platform_support');

                    IF unknown_roles IS NOT NULL THEN
                        RAISE EXCEPTION 'Unknown platform role(s): %', unknown_roles;
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM staff_role_assignments
                        GROUP BY "StaffUserId", "OrganizationId", "BranchId",
                            CASE "RoleName"
                                WHEN 'owner' THEN 'organization_owner'
                                WHEN 'cashier_operator' THEN 'operator'
                                WHEN 'accountant_auditor' THEN 'accountant'
                                ELSE "RoleName"
                            END
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Organization role mapping would create duplicate staff assignments';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM staff_money_caps
                        GROUP BY "BranchId", "ActionScope",
                            CASE "RoleName"
                                WHEN 'owner' THEN 'organization_owner'
                                WHEN 'cashier_operator' THEN 'operator'
                                WHEN 'accountant_auditor' THEN 'accountant'
                                ELSE "RoleName"
                            END
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Organization role mapping would create duplicate money caps';
                    END IF;
                END
                $cutover$;

                UPDATE staff_role_assignments
                SET "RoleName" = CASE "RoleName"
                    WHEN 'owner' THEN 'organization_owner'
                    WHEN 'cashier_operator' THEN 'operator'
                    WHEN 'accountant_auditor' THEN 'accountant'
                    ELSE "RoleName"
                END;

                UPDATE staff_money_caps
                SET "RoleName" = CASE "RoleName"
                    WHEN 'owner' THEN 'organization_owner'
                    WHEN 'cashier_operator' THEN 'operator'
                    WHEN 'accountant_auditor' THEN 'accountant'
                    ELSE "RoleName"
                END;

                UPDATE staff_invites
                SET "RoleNamesCsv" = (
                    SELECT string_agg(
                        CASE btrim(role_name)
                            WHEN 'owner' THEN 'organization_owner'
                            WHEN 'cashier_operator' THEN 'operator'
                            WHEN 'accountant_auditor' THEN 'accountant'
                            ELSE btrim(role_name)
                        END,
                        ',' ORDER BY ordinality) AS roles
                    FROM regexp_split_to_table(staff_invites."RoleNamesCsv", ',')
                        WITH ORDINALITY AS split(role_name, ordinality)
                );

                UPDATE platform_admin_users
                SET "RolesJson" = COALESCE((
                    SELECT jsonb_agg(
                        CASE role_name
                            WHEN 'platform_owner' THEN 'platform_admin'
                            ELSE role_name
                        END
                        ORDER BY ordinality) AS roles
                    FROM jsonb_array_elements_text(platform_admin_users."RolesJson")
                        WITH ORDINALITY AS split(role_name, ordinality)
                ), '[]'::jsonb);

                UPDATE staff_access_tokens
                SET "RevokedAtUtc" = CURRENT_TIMESTAMP
                WHERE "RevokedAtUtc" IS NULL;

                UPDATE staff_refresh_tokens
                SET "RevokedAtUtc" = CURRENT_TIMESTAMP
                WHERE "RevokedAtUtc" IS NULL;

                UPDATE platform_admin_access_tokens
                SET "RevokedAtUtc" = CURRENT_TIMESTAMP
                WHERE "RevokedAtUtc" IS NULL;

                UPDATE platform_admin_refresh_tokens
                SET "RevokedAtUtc" = CURRENT_TIMESTAMP
                WHERE "RevokedAtUtc" IS NULL;

                UPDATE player_access_tokens
                SET "RevokedAtUtc" = CURRENT_TIMESTAMP
                WHERE "RevokedAtUtc" IS NULL;

                UPDATE player_refresh_tokens
                SET "RevokedAtUtc" = CURRENT_TIMESTAMP
                WHERE "RevokedAtUtc" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "The platform/organization identity cutover cannot be reversed. Restore the pre-cutover database snapshot.");
        }
    }
}
