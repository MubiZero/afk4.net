using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_feature_overrides",
                columns: table => new
                {
                    OrganizationFeatureOverrideId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SetByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_feature_overrides", x => x.OrganizationFeatureOverrideId);
                });

            migrationBuilder.CreateTable(
                name: "plan_features",
                columns: table => new
                {
                    PlanFeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FeatureKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsIncluded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_features", x => x.PlanFeatureId);
                });

            migrationBuilder.CreateTable(
                name: "platform_features",
                columns: table => new
                {
                    FeatureKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EnabledByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_features", x => x.FeatureKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_feature_overrides_Organization_Feature",
                table: "organization_feature_overrides",
                columns: new[] { "OrganizationId", "FeatureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_features_Plan_Feature",
                table: "plan_features",
                columns: new[] { "PlanCode", "FeatureKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_feature_overrides");

            migrationBuilder.DropTable(
                name: "plan_features");

            migrationBuilder.DropTable(
                name: "platform_features");
        }
    }
}
