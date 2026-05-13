using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingLedgerTariffsPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_command_idempotency",
                columns: table => new
                {
                    BillingCommandIdempotencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_command_idempotency", x => x.BillingCommandIdempotencyId);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlayerPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntryType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    QuantitySeconds = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ReversesLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.LedgerEntryId);
                });

            migrationBuilder.CreateTable(
                name: "package_definitions",
                columns: table => new
                {
                    PackageDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PriceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    IncludedSeconds = table.Column<int>(type: "integer", nullable: false),
                    BonusSeconds = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAfterDays = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_definitions", x => x.PackageDefinitionId);
                });

            migrationBuilder.CreateTable(
                name: "player_accounts",
                columns: table => new
                {
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_accounts", x => x.PlayerAccountId);
                });

            migrationBuilder.CreateTable(
                name: "player_packages",
                columns: table => new
                {
                    PlayerPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PurchasedPriceMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    IncludedSeconds = table.Column<int>(type: "integer", nullable: false),
                    BonusSeconds = table.Column<int>(type: "integer", nullable: false),
                    PurchasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_packages", x => x.PlayerPackageId);
                });

            migrationBuilder.CreateTable(
                name: "tariff_versions",
                columns: table => new
                {
                    TariffVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TariffId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PricePerMinuteMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    MinimumBillableMinutes = table.Column<int>(type: "integer", nullable: false),
                    RoundingIncrementMinutes = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff_versions", x => x.TariffVersionId);
                });

            migrationBuilder.CreateTable(
                name: "tariffs",
                columns: table => new
                {
                    TariffId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariffs", x => x.TariffId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_command_idempotency_OrganizationId_BranchId_Operati~",
                table: "billing_command_idempotency",
                columns: new[] { "OrganizationId", "BranchId", "Operation", "IdempotencyKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_OrganizationId_BranchId_CreatedAtUtc",
                table: "ledger_entries",
                columns: new[] { "OrganizationId", "BranchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PlayerAccountId_CreatedAtUtc",
                table: "ledger_entries",
                columns: new[] { "PlayerAccountId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_PlayerPackageId",
                table: "ledger_entries",
                column: "PlayerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_ReversesLedgerEntryId",
                table: "ledger_entries",
                column: "ReversesLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_SessionId",
                table: "ledger_entries",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_package_definitions_OrganizationId_BranchId_Name",
                table: "package_definitions",
                columns: new[] { "OrganizationId", "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_accounts_OrganizationId_HomeBranchId",
                table: "player_accounts",
                columns: new[] { "OrganizationId", "HomeBranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_player_packages_OrganizationId_BranchId",
                table: "player_packages",
                columns: new[] { "OrganizationId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_player_packages_PlayerAccountId_PurchasedAtUtc",
                table: "player_packages",
                columns: new[] { "PlayerAccountId", "PurchasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_tariff_versions_OrganizationId_BranchId_EffectiveFromUtc",
                table: "tariff_versions",
                columns: new[] { "OrganizationId", "BranchId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_tariff_versions_TariffId_VersionNumber",
                table: "tariff_versions",
                columns: new[] { "TariffId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tariffs_OrganizationId_BranchId_Name",
                table: "tariffs",
                columns: new[] { "OrganizationId", "BranchId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_command_idempotency");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "package_definitions");

            migrationBuilder.DropTable(
                name: "player_accounts");

            migrationBuilder.DropTable(
                name: "player_packages");

            migrationBuilder.DropTable(
                name: "tariff_versions");

            migrationBuilder.DropTable(
                name: "tariffs");
        }
    }
}
