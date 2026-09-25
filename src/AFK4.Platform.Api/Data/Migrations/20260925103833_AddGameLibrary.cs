using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_game_libraries",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_game_libraries", x => x.BranchId);
                });

            migrationBuilder.CreateTable(
                name: "branch_games",
                columns: table => new
                {
                    BranchGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Genre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    MinAge = table.Column<int>(type: "integer", nullable: true),
                    CoverUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    LaunchKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LaunchTarget = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExecutablePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Arguments = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AvailableWithoutSession = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_games", x => x.BranchGameId);
                });

            migrationBuilder.CreateTable(
                name: "catalog_games",
                columns: table => new
                {
                    CatalogGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Genre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    MinAge = table.Column<int>(type: "integer", nullable: true),
                    LaunchKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LaunchTarget = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CoverUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByPlatformAdminUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_games", x => x.CatalogGameId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branch_game_libraries_OrganizationId",
                table: "branch_game_libraries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_games_BranchId_SortOrder",
                table: "branch_games",
                columns: new[] { "BranchId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_branch_games_CatalogGameId",
                table: "branch_games",
                column: "CatalogGameId");

            migrationBuilder.CreateIndex(
                name: "IX_branch_games_OrganizationId",
                table: "branch_games",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_games_Name",
                table: "catalog_games",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_game_libraries");

            migrationBuilder.DropTable(
                name: "branch_games");

            migrationBuilder.DropTable(
                name: "catalog_games");
        }
    }
}
