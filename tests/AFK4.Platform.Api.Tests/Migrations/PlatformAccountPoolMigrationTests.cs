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
