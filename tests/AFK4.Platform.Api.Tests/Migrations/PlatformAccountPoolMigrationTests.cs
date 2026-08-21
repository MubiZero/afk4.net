using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Миграция общего котла платформы на настоящей PostgreSQL: накатывается на чистую базу и
/// откатывается до предыдущей, не оставляя за собой ни таблиц, ни колонок. In-memory провайдер
/// такое не проверяет вовсе — он миграции не исполняет.
/// </summary>
public sealed class PlatformAccountPoolMigrationTests
{
    private const string PreviousMigration = "AddTariffSchedule";
    private const string ThisMigration = "AddPlatformAccountPool";

    [MigrationPostgresFact]
    public async Task Down_ReturnsTheSchemaToWhereItWas()
    {
        await using var schema = await MigrationSchema.CreateAsync("account_pool");
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
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("player_accounts", "PlatformPersonId")));
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("player_accounts", "CreatedFromApp")));
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("reservations", "RespondByUtc")));
        Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql("reservations", "ConfirmedAtUtc")));
    }

    private static string TableExistsSql(string table) => $"""
        SELECT count(*)::text FROM information_schema.tables
        WHERE table_schema = current_schema() AND table_name = '{table}'
        """;

    private static string ColumnExistsSql(string table, string column) => $"""
        SELECT count(*)::text FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = '{table}' AND column_name = '{column}'
        """;
}
