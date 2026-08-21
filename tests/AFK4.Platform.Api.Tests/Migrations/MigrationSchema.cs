using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Одноразовая схема настоящей PostgreSQL, по которой можно ходить миграциями вперёд и назад.
/// </summary>
public sealed class MigrationSchema : IAsyncDisposable
{
    private readonly string connectionString;
    private readonly string schemaName;

    private MigrationSchema(string connectionString, string schemaName)
    {
        this.connectionString = connectionString;
        this.schemaName = schemaName;
    }

public static async Task<MigrationSchema> CreateAsync(string namePrefix)
    {
        var builder = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable(MigrationPostgresFactAttribute.EnvironmentVariable)!);
        var schemaName = $"{namePrefix}_{Guid.NewGuid():N}";
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
