using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Перенос клубных карточек на личности, проверенный на настоящей PostgreSQL и на данных, которые
/// база сегодня действительно содержит: один номер в двух клубах, один номер дважды в одном клубе,
/// карточка без телефона и телефон, который номером не является.
///
/// Тест накатывает миграции до предыдущей, заполняет таблицы так, как они выглядят до перехода, и
/// только потом применяет саму миграцию. Иначе проверялся бы перенос пустой базы, то есть ничего.
/// </summary>
public sealed class PlatformAccountPoolMigrationTests
{
    private const string PreviousMigration = "AddTariffSchedule";
    private const string ThisMigration = "AddPlatformAccountPool";

    private static readonly Guid ClubA = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid ClubB = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid ClubC = Guid.Parse("a0000000-0000-4000-8000-000000000003");

    private static readonly Guid TwoClubsFirst = Guid.Parse("b0000000-0000-4000-8000-000000000001");
    private static readonly Guid TwoClubsSecond = Guid.Parse("b0000000-0000-4000-8000-000000000002");
    private static readonly Guid PhonelessGuest = Guid.Parse("b0000000-0000-4000-8000-000000000003");
    private static readonly Guid UnusablePhone = Guid.Parse("b0000000-0000-4000-8000-000000000004");
    private static readonly Guid DuplicateWithoutSession = Guid.Parse("b0000000-0000-4000-8000-000000000005");
    private static readonly Guid DuplicateWithSession = Guid.Parse("b0000000-0000-4000-8000-000000000006");
    private static readonly Guid DeactivatedTwin = Guid.Parse("b0000000-0000-4000-8000-000000000007");

    [MigrationPostgresFact]
    public async Task Cutover_GluesByPhone_RecordsWhatItCannotDecide_AndTouchesNoMoney()
    {
        await using var schema = await MigrationSchema.CreateAsync();
        await schema.MigrateToAsync(PreviousMigration);
        await schema.ExecuteAsync(SeedSql);

        var ledgerBefore = await schema.ScalarAsync(LedgerFingerprintSql);
        await schema.MigrateToAsync(ThisMigration);

        // Один номер в двух клубах — одна личность и оба счёта при ней.
        var glued = await schema.ScalarAsync($"""
            SELECT count(DISTINCT "PlatformPersonId")::text
            FROM player_accounts
            WHERE "PlayerAccountId" IN ('{TwoClubsFirst}', '{TwoClubsSecond}')
              AND "PlatformPersonId" IS NOT NULL
            """);
        Assert.Equal("1", glued);
        Assert.Equal("2", await schema.ScalarAsync($"""
            SELECT count(*)::text FROM player_accounts
            WHERE "PlayerAccountId" IN ('{TwoClubsFirst}', '{TwoClubsSecond}')
              AND "PlatformPersonId" IS NOT NULL
            """));

        // Имя и язык — с самого свежего счёта, у которого имя есть.
        Assert.Equal("Фаррух Носиров|tg", await schema.ScalarAsync("""
            SELECT "DisplayName" || '|' || coalesce("PreferredLocale", '-')
            FROM platform_persons WHERE "PhoneNumber" = '+992900000001'
            """));

        // Подтверждён однажды — подтверждён: берём самое раннее подтверждение по всем клубам.
        Assert.Equal("2026-04-01", await schema.ScalarAsync("""
            SELECT to_char("PhoneVerifiedAtUtc" AT TIME ZONE 'UTC', 'YYYY-MM-DD')
            FROM platform_persons WHERE "PhoneNumber" = '+992900000001'
            """));

        // Расхождение имён склейку не блокирует, но становится видимым.
        Assert.Equal("1", await schema.ScalarAsync("""
            SELECT count(*)::text FROM platform_identity_migration_findings
            WHERE "Kind" = 'name_mismatch'
            """));

        // Гость без телефона живёт как жил.
        Assert.Equal("", await schema.ScalarAsync($"""
            SELECT coalesce("PlatformPersonId"::text, '')
            FROM player_accounts WHERE "PlayerAccountId" = '{PhonelessGuest}'
            """));
        Assert.Equal("0", await schema.ScalarAsync($"""
            SELECT count(*)::text FROM platform_identity_migration_findings
            WHERE "PlayerAccountId" = '{PhonelessGuest}'
            """));

        // Нераспознаваемый номер перенос не роняет: счёт остаётся клубным, случай записан.
        Assert.Equal("", await schema.ScalarAsync($"""
            SELECT coalesce("PlatformPersonId"::text, '')
            FROM player_accounts WHERE "PlayerAccountId" = '{UnusablePhone}'
            """));
        Assert.Equal("1", await schema.ScalarAsync($"""
            SELECT count(*)::text FROM platform_identity_migration_findings
            WHERE "Kind" = 'unusable_phone' AND "PlayerAccountId" = '{UnusablePhone}'
            """));

        // Один номер дважды в одном клубе: подшивается счёт с самой свежей сессией, второй ждёт
        // человека — уникальный индекс при этом не нарушен.
        Assert.Equal("1", await schema.ScalarAsync($"""
            SELECT count(*)::text FROM player_accounts
            WHERE "PlayerAccountId" = '{DuplicateWithSession}' AND "PlatformPersonId" IS NOT NULL
            """));
        Assert.Equal("", await schema.ScalarAsync($"""
            SELECT coalesce("PlatformPersonId"::text, '')
            FROM player_accounts WHERE "PlayerAccountId" = '{DuplicateWithoutSession}'
            """));
        Assert.Equal("1", await schema.ScalarAsync($"""
            SELECT count(*)::text FROM platform_identity_migration_findings
            WHERE "Kind" = 'duplicate_in_club' AND "PlayerAccountId" = '{DuplicateWithoutSession}'
            """));

        // Отключённая карточка личности не получает: она не участвует ни в склейке, ни в спорах.
        Assert.Equal("", await schema.ScalarAsync($"""
            SELECT coalesce("PlatformPersonId"::text, '')
            FROM player_accounts WHERE "PlayerAccountId" = '{DeactivatedTwin}'
            """));

        // Личностей ровно столько, сколько различимых номеров среди активных счетов.
        Assert.Equal("2", await schema.ScalarAsync("SELECT count(*)::text FROM platform_persons"));

        // Ни один клубный PIN не промотирован в сетевой.
        Assert.Equal("0", await schema.ScalarAsync("""
            SELECT count(*)::text FROM platform_persons WHERE "PinHash" IS NOT NULL
            """));
        Assert.Equal("1", await schema.ScalarAsync("""
            SELECT count(*)::text FROM player_credentials WHERE "PasswordHash" IS NOT NULL
            """));

        // У каждой находки есть дата: очередь разбора живёт месяцами, и без неё в ней нет порядка.
        Assert.Equal("0", await schema.ScalarAsync("""
            SELECT count(*)::text FROM platform_identity_migration_findings
            WHERE "CreatedAtUtc" IS NULL
            """));
        Assert.Equal("3", await schema.ScalarAsync("""
            SELECT count(*)::text FROM platform_identity_migration_findings
            WHERE "CreatedAtUtc" > CURRENT_TIMESTAMP - interval '1 hour' AND "ResolvedAtUtc" IS NULL
            """));

        // Главный инвариант: миграция денег не касается.
        Assert.Equal(ledgerBefore, await schema.ScalarAsync(LedgerFingerprintSql));
    }

    [MigrationPostgresFact]
    public async Task Down_ReturnsTheSchemaToWhereItWas()
    {
        await using var schema = await MigrationSchema.CreateAsync();
        await schema.MigrateToAsync(ThisMigration);
        Assert.Equal("1", await schema.ScalarAsync(TableExistsSql("platform_persons")));
        Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql("player_accounts", "PlatformPersonId")));

        await schema.MigrateToAsync(PreviousMigration);

        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("platform_persons")));
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("platform_person_access_tokens")));
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("platform_person_refresh_tokens")));
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("platform_phone_otps")));
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("branch_booking_settings")));
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("platform_reputation_snapshots")));
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("platform_identity_migration_findings")));
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("player_accounts", "PlatformPersonId")));
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("player_accounts", "CreatedFromApp")));
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("reservations", "RespondByUtc")));
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("reservations", "ConfirmedAtUtc")));
    }

    /// <summary>
    /// Предполётная проверка из плана: пять чисел, которые называют цену переноса до того, как он
    /// случится. Живёт в тесте, а не отдельным скриптом, чтобы не разъехаться с реальностью.
    /// </summary>
    [MigrationPostgresFact]
    public async Task Preflight_CountsWhatTheCutoverWillCost()
    {
        await using var schema = await MigrationSchema.CreateAsync();
        await schema.MigrateToAsync(PreviousMigration);
        await schema.ExecuteAsync(SeedSql);

        Assert.Equal("1", await schema.ScalarAsync(PreflightSql.PhonesInMoreThanOneClub));
        Assert.Equal("1", await schema.ScalarAsync(PreflightSql.DuplicatesInsideOneClub));
        Assert.Equal("1", await schema.ScalarAsync(PreflightSql.AccountsWithoutPhone));
        Assert.Equal("1", await schema.ScalarAsync(PreflightSql.UnusablePhones));
        Assert.Equal("1", await schema.ScalarAsync(PreflightSql.PhonesWithConflictingNames));
    }

    private const string LedgerFingerprintSql = """
        SELECT coalesce(md5(string_agg(
            "LedgerEntryId"::text || '|' || "PlayerAccountId"::text || '|' ||
            "EntryType" || '|' || "AmountMinorUnits"::text, '/' ORDER BY "LedgerEntryId")), 'empty')
            || '#' || count(*)::text
        FROM ledger_entries
        """;

    private static string TableExistsSql(string table) => $"""
        SELECT count(*)::text FROM information_schema.tables
        WHERE table_schema = current_schema() AND table_name = '{table}'
        """;

    private static string ColumnExistsSql(string table, string column) => $"""
        SELECT count(*)::text FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = '{table}' AND column_name = '{column}'
        """;

    private static class PreflightSql
    {
        public const string PhonesInMoreThanOneClub = """
            SELECT count(*)::text FROM (
                SELECT "PhoneNumber" FROM player_accounts
                WHERE "IsActive" AND "PhoneNumber" ~ '^\+[0-9]{11,15}$'
                GROUP BY "PhoneNumber" HAVING count(DISTINCT "OrganizationId") > 1) multi
            """;

        public const string DuplicatesInsideOneClub = """
            SELECT coalesce(sum(extra), 0)::text FROM (
                SELECT count(*) - 1 AS extra FROM player_accounts
                WHERE "IsActive" AND "PhoneNumber" ~ '^\+[0-9]{11,15}$'
                GROUP BY "OrganizationId", "PhoneNumber" HAVING count(*) > 1) duplicates
            """;

        public const string AccountsWithoutPhone = """
            SELECT count(*)::text FROM player_accounts
            WHERE "IsActive" AND coalesce(btrim("PhoneNumber"), '') = ''
            """;

        public const string UnusablePhones = """
            SELECT count(*)::text FROM player_accounts
            WHERE "IsActive" AND coalesce(btrim("PhoneNumber"), '') <> ''
              AND "PhoneNumber" !~ '^\+[0-9]{11,15}$'
            """;

        public const string PhonesWithConflictingNames = """
            SELECT count(*)::text FROM (
                SELECT "PhoneNumber" FROM player_accounts
                WHERE "IsActive" AND "PhoneNumber" ~ '^\+[0-9]{11,15}$' AND btrim("DisplayName") <> ''
                GROUP BY "PhoneNumber" HAVING count(DISTINCT btrim("DisplayName")) > 1) conflicting
            """;
    }

    private static string SeedSql => $"""
        INSERT INTO player_accounts
            ("PlayerAccountId", "OrganizationId", "HomeBranchId", "DisplayName", "PhoneNumber",
             "PreferredLocale", "MarketingOptIn", "IsActive", "CreatedAtUtc")
        VALUES
            ('{TwoClubsFirst}', '{ClubA}', '{ClubA}', 'Фаррух', '+992900000001', 'ru', false, true,
             '2026-01-01T00:00:00Z'),
            ('{TwoClubsSecond}', '{ClubB}', '{ClubB}', 'Фаррух Носиров', '+992900000001', 'tg', false, true,
             '2026-02-01T00:00:00Z'),
            ('{PhonelessGuest}', '{ClubA}', '{ClubA}', 'Гость со стойки', NULL, NULL, false, true,
             '2026-01-05T00:00:00Z'),
            ('{UnusablePhone}', '{ClubA}', '{ClubA}', 'Кривой номер', '93-738', NULL, false, true,
             '2026-01-06T00:00:00Z'),
            ('{DuplicateWithoutSession}', '{ClubC}', '{ClubC}', 'Азиз', '+992900000005', NULL, false, true,
             '2026-03-01T00:00:00Z'),
            ('{DuplicateWithSession}', '{ClubC}', '{ClubC}', 'Азиз', '+992900000005', NULL, false, true,
             '2026-01-01T00:00:00Z'),
            ('{DeactivatedTwin}', '{ClubB}', '{ClubB}', 'Закрытая карточка', '+992900000001', NULL, false, false,
             '2026-01-02T00:00:00Z');

        INSERT INTO player_credentials
            ("PlayerCredentialId", "PlayerAccountId", "OrganizationId", "PasswordHash", "PhoneVerified",
             "PhoneVerifiedAtUtc", "FailedLoginCount", "CreatedAtUtc", "UpdatedAtUtc")
        VALUES
            (gen_random_uuid(), '{TwoClubsFirst}', '{ClubA}', 'club-pin-hash', true,
             '2026-05-01T00:00:00Z', 0, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
            (gen_random_uuid(), '{TwoClubsSecond}', '{ClubB}', NULL, true,
             '2026-04-01T00:00:00Z', 0, '2026-02-01T00:00:00Z', '2026-02-01T00:00:00Z');

        INSERT INTO sessions
            ("SessionId", "OrganizationId", "BranchId", "SeatId", "DeviceId", "CreatedByStaffUserId",
             "PlayerKind", "PlayerAccountId", "TariffRuleVersionId", "BillingMode", "State",
             "RequestedAtUtc", "StartedAtUtc", "UpdatedAtUtc", "IsComp", "Version")
        VALUES
            (gen_random_uuid(), '{ClubC}', '{ClubC}', gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
             'member', '{DuplicateWithSession}', 'v1', 'prepaid_wallet', 'ended',
             '2026-06-01T00:00:00Z', '2026-06-01T00:00:00Z', '2026-06-01T02:00:00Z', false, 1);

        INSERT INTO ledger_entries
            ("LedgerEntryId", "OrganizationId", "BranchId", "PlayerAccountId", "EntryType", "AccountType",
             "AmountMinorUnits", "QuantitySeconds", "CurrencyCode", "Description", "Reason",
             "CreatedByStaffUserId", "CreatedAtUtc")
        VALUES
            (gen_random_uuid(), '{ClubA}', '{ClubA}', '{TwoClubsFirst}', 'top_up', 'wallet',
             10000, 0, 'TJS', 'Пополнение', 'seed', gen_random_uuid(), '2026-01-02T00:00:00Z'),
            (gen_random_uuid(), '{ClubC}', '{ClubC}', '{DuplicateWithSession}', 'gameplay_charge', 'wallet',
             -2500, 3600, 'TJS', 'Игра', 'seed', gen_random_uuid(), '2026-06-01T02:00:00Z');
        """;

    /// <summary>
    /// Одноразовая схема настоящей PostgreSQL, по которой можно ходить миграциями вперёд и назад.
    /// </summary>
    private sealed class MigrationSchema : IAsyncDisposable
    {
        private readonly string connectionString;
        private readonly string schemaName;

        private MigrationSchema(string connectionString, string schemaName)
        {
            this.connectionString = connectionString;
            this.schemaName = schemaName;
        }

        public static async Task<MigrationSchema> CreateAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable(MigrationPostgresFactAttribute.EnvironmentVariable)!);
            var schemaName = $"account_pool_{Guid.NewGuid():N}";
            await using (var connection = new NpgsqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
                await command.ExecuteNonQueryAsync();
            }

            builder.SearchPath = schemaName;
            return new MigrationSchema(builder.ConnectionString, schemaName);
        }

        public async Task MigrateToAsync(string migration)
        {
            await using var db = CreateDbContext();
            await db.GetService<IMigrator>().MigrateAsync(migration);
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<string> ScalarAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull
                ? string.Empty
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
        }

        private PlatformDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connectionString).Options);

        public async ValueTask DisposeAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = null };
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }
}
