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
                name: "platform_identity_migration_findings",
                columns: table => new
                {
                    FindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlatformPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_identity_migration_findings", x => x.FindingId);
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
                name: "IX_platform_identity_migration_findings_Kind_ResolvedAtUtc",
                table: "platform_identity_migration_findings",
                columns: new[] { "Kind", "ResolvedAtUtc" });

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

            // Перенос клубных карточек на личности. Он ничего не блокирует и ничего не решает за
            // человека: спорные случаи записываются в platform_identity_migration_findings и
            // разбираются руками после. Денег он не касается вовсе — ни одной строки
            // ledger_entries здесь нет и быть не может.
            migrationBuilder.Sql(
                """
                DO $cutover$
                BEGIN
                    -- Личность на каждый номер, который вообще может быть номером. Имя и язык
                    -- берём с самого свежего счёта, у которого имя есть: свежее — вероятнее
                    -- актуальное. Подтверждение номера — самое раннее по всем клубам:
                    -- подтверждён однажды — подтверждён.
                    --
                    -- PinHash остаётся NULL у всех без исключения. Клубный PIN назначал админ, и
                    -- промотировать его до сетевого — значит выдать админу одного клуба вход от
                    -- чужого имени во всех остальных.
                    INSERT INTO platform_persons (
                        "PlatformPersonId", "PhoneNumber", "DisplayName", "PreferredLocale",
                        "PhoneVerifiedAtUtc", "PinHash", "PinSetAtUtc", "PinFailedCount",
                        "PinLockedUntilUtc", "NetworkBanAtUtc", "NetworkBanReason",
                        "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                    SELECT
                        gen_random_uuid(),
                        named.phone,
                        named.display_name,
                        named.preferred_locale,
                        verified.verified_at,
                        NULL, NULL, 0, NULL, NULL, NULL,
                        true,
                        CURRENT_TIMESTAMP,
                        CURRENT_TIMESTAMP
                    FROM (
                        SELECT DISTINCT ON (account."PhoneNumber")
                            account."PhoneNumber" AS phone,
                            account."DisplayName" AS display_name,
                            account."PreferredLocale" AS preferred_locale
                        FROM player_accounts account
                        WHERE account."IsActive"
                          AND account."PhoneNumber" ~ '^\+[0-9]{11,15}$'
                        ORDER BY
                            account."PhoneNumber",
                            (btrim(account."DisplayName") <> '') DESC,
                            account."CreatedAtUtc" DESC,
                            account."PlayerAccountId" DESC
                    ) named
                    LEFT JOIN LATERAL (
                        SELECT min(credential."PhoneVerifiedAtUtc") AS verified_at
                        FROM player_accounts sibling
                        JOIN player_credentials credential
                          ON credential."PlayerAccountId" = sibling."PlayerAccountId"
                        WHERE sibling."IsActive"
                          AND sibling."PhoneNumber" = named.phone
                          AND credential."PhoneVerified"
                    ) verified ON true;

                    -- Подшиваем счета. В каждом клубе к личности едет ровно один: слить два счёта
                    -- значит подвинуть деньги между кошельками, и молча этого не делает никто.
                    -- Побеждает счёт с самой свежей сессией, при равенстве — заведённый позже.
                    WITH attached AS (
                        SELECT DISTINCT ON (account."OrganizationId", account."PhoneNumber")
                            account."PlayerAccountId" AS player_account_id,
                            person."PlatformPersonId" AS platform_person_id
                        FROM player_accounts account
                        JOIN platform_persons person
                          ON person."PhoneNumber" = account."PhoneNumber"
                        LEFT JOIN LATERAL (
                            SELECT max(coalesce(session."StartedAtUtc", session."RequestedAtUtc")) AS last_seen
                            FROM sessions session
                            WHERE session."PlayerAccountId" = account."PlayerAccountId"
                        ) latest ON true
                        WHERE account."IsActive"
                        ORDER BY
                            account."OrganizationId",
                            account."PhoneNumber",
                            latest.last_seen DESC NULLS LAST,
                            account."CreatedAtUtc" DESC,
                            account."PlayerAccountId" DESC
                    )
                    UPDATE player_accounts account
                    SET "PlatformPersonId" = attached.platform_person_id
                    FROM attached
                    WHERE account."PlayerAccountId" = attached.player_account_id;

                    -- Находка: один номер дважды в одном клубе. Ничего не потеряно — оператор
                    -- видит обе карточки, как и вчера, — но в приложении человек до ручного
                    -- слияния увидит только один из двух счетов.
                    INSERT INTO platform_identity_migration_findings (
                        "FindingId", "Kind", "PlatformPersonId", "PlayerAccountId",
                        "OrganizationId", "DetailsJson", "ResolvedAtUtc")
                    SELECT
                        gen_random_uuid(),
                        'duplicate_in_club',
                        person."PlatformPersonId",
                        account."PlayerAccountId",
                        account."OrganizationId",
                        jsonb_build_object(
                            'phone', account."PhoneNumber",
                            'displayName', account."DisplayName",
                            'attachedPlayerAccountId', attached."PlayerAccountId"),
                        NULL
                    FROM player_accounts account
                    JOIN platform_persons person
                      ON person."PhoneNumber" = account."PhoneNumber"
                    JOIN player_accounts attached
                      ON attached."OrganizationId" = account."OrganizationId"
                     AND attached."PlatformPersonId" = person."PlatformPersonId"
                    WHERE account."IsActive"
                      AND account."PlatformPersonId" IS NULL;

                    -- Находка: один номер, разные имена. Склейку это не блокирует — так видно
                    -- случаи «один номер, два человека», и разбирает их живой человек.
                    INSERT INTO platform_identity_migration_findings (
                        "FindingId", "Kind", "PlatformPersonId", "PlayerAccountId",
                        "OrganizationId", "DetailsJson", "ResolvedAtUtc")
                    SELECT
                        gen_random_uuid(),
                        'name_mismatch',
                        person."PlatformPersonId",
                        NULL,
                        NULL,
                        jsonb_build_object('phone', person."PhoneNumber", 'names', names.seen),
                        NULL
                    FROM platform_persons person
                    JOIN LATERAL (
                        SELECT
                            jsonb_agg(DISTINCT btrim(account."DisplayName")) AS seen,
                            count(DISTINCT btrim(account."DisplayName")) AS distinct_names
                        FROM player_accounts account
                        WHERE account."IsActive"
                          AND account."PhoneNumber" = person."PhoneNumber"
                          AND btrim(account."DisplayName") <> ''
                    ) names ON true
                    WHERE names.distinct_names > 1;

                    -- Находка: номер, который номером не является. Нормализовать вслепую значит
                    -- склеить не тех, а ронять перенос из-за трёх кривых строк — тоже не дело.
                    INSERT INTO platform_identity_migration_findings (
                        "FindingId", "Kind", "PlatformPersonId", "PlayerAccountId",
                        "OrganizationId", "DetailsJson", "ResolvedAtUtc")
                    SELECT
                        gen_random_uuid(),
                        'unusable_phone',
                        NULL,
                        account."PlayerAccountId",
                        account."OrganizationId",
                        jsonb_build_object(
                            'phone', account."PhoneNumber",
                            'displayName', account."DisplayName"),
                        NULL
                    FROM player_accounts account
                    WHERE account."IsActive"
                      AND coalesce(btrim(account."PhoneNumber"), '') <> ''
                      AND account."PhoneNumber" !~ '^\+[0-9]{11,15}$';
                END
                $cutover$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branch_booking_settings");

            migrationBuilder.DropTable(
                name: "platform_identity_migration_findings");

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
