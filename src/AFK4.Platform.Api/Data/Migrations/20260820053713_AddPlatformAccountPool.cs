using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFK4.Platform.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAccountPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondByUtc",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreatedFromApp",
                table: "player_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PlatformPersonId",
                table: "player_accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "branch_booking_settings",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptanceMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RespondWithinMinutes = table.Column<int>(type: "integer", nullable: false),
                    RequirePrepaymentFromNewGuests = table.Column<bool>(type: "boolean", nullable: false),
                    MaxActiveReservationsForNewGuests = table.Column<int>(type: "integer", nullable: false),
                    RegularAfterVisits = table.Column<int>(type: "integer", nullable: false),
                    HoldSeatAfterStartMinutes = table.Column<int>(type: "integer", nullable: false),
                    KeepPrepaymentOnNoShow = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedByStaffUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_booking_settings", x => x.BranchId);
                });

            migrationBuilder.CreateTable(
                name: "platform_person_access_tokens",
                columns: table => new
                {
                    PlatformPersonAccessTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PinnedOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_person_access_tokens", x => x.PlatformPersonAccessTokenId);
                });

            migrationBuilder.CreateTable(
                name: "platform_person_refresh_tokens",
                columns: table => new
                {
                    PlatformPersonRefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PinnedOrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_person_refresh_tokens", x => x.PlatformPersonRefreshTokenId);
                });

            migrationBuilder.CreateTable(
                name: "platform_persons",
                columns: table => new
                {
                    PlatformPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PreferredLocale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PhoneVerifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PinHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PinSetAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PinFailedCount = table.Column<int>(type: "integer", nullable: false),
                    PinLockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NetworkBanAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NetworkBanReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_persons", x => x.PlatformPersonId);
                });

            migrationBuilder.CreateTable(
                name: "platform_phone_otps",
                columns: table => new
                {
                    PlatformPhoneOtpId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_phone_otps", x => x.PlatformPhoneOtpId);
                });

            migrationBuilder.CreateTable(
                name: "platform_reputation_snapshots",
                columns: table => new
                {
                    PlatformPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetworkVisits = table.Column<int>(type: "integer", nullable: false),
                    NetworkNoShows = table.Column<int>(type: "integer", nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_reputation_snapshots", x => x.PlatformPersonId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_accounts_PlatformPersonId",
                table: "player_accounts",
                column: "PlatformPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_player_accounts_PlatformPersonId_OrganizationId",
                table: "player_accounts",
                columns: new[] { "PlatformPersonId", "OrganizationId" },
                unique: true,
                filter: "\"PlatformPersonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_branch_booking_settings_OrganizationId",
                table: "branch_booking_settings",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_person_access_tokens_PlatformPersonId_ExpiresAtUtc",
                table: "platform_person_access_tokens",
                columns: new[] { "PlatformPersonId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_person_access_tokens_TokenHash",
                table: "platform_person_access_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_platform_person_refresh_tokens_PlatformPersonId_ExpiresAtUtc",
                table: "platform_person_refresh_tokens",
                columns: new[] { "PlatformPersonId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_person_refresh_tokens_TokenHash",
                table: "platform_person_refresh_tokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_platform_persons_PhoneNumber",
                table: "platform_persons",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_phone_otps_Phone_Purpose_CreatedAtUtc",
                table: "platform_phone_otps",
                columns: new[] { "Phone", "Purpose", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_booking_settings");

            migrationBuilder.DropTable(
                name: "platform_person_access_tokens");

            migrationBuilder.DropTable(
                name: "platform_person_refresh_tokens");

            migrationBuilder.DropTable(
                name: "platform_persons");

            migrationBuilder.DropTable(
                name: "platform_phone_otps");

            migrationBuilder.DropTable(
                name: "platform_reputation_snapshots");

            migrationBuilder.DropIndex(
                name: "IX_player_accounts_PlatformPersonId",
                table: "player_accounts");

            migrationBuilder.DropIndex(
                name: "IX_player_accounts_PlatformPersonId_OrganizationId",
                table: "player_accounts");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "RespondByUtc",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "CreatedFromApp",
                table: "player_accounts");

            migrationBuilder.DropColumn(
                name: "PlatformPersonId",
                table: "player_accounts");
        }
    }
}
