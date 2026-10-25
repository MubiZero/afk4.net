using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Токен сотрудника и администратора ищется по хешу в SQL (`"TokenHash" = @p`), а не перебором всех
/// живых токенов в памяти. InMemory сравнивает массивы байтов по значению сам и такую ошибку не
/// покажет, поэтому трансляция проверяется на настоящем PostgreSQL: запрос обязан выполниться и
/// сравнивать именно байты, иначе вход всех сотрудников сломался бы только на сервере.
/// </summary>
public sealed class TokenHashLookupPostgresTests
{
    [PlatformAdminPostgresFact]
    public async Task StaffTokenLookup_IsAByteaEqualityInSql_AndFindsTheTokenByItsBytes()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var schema = $"token_lookup_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(connectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString)
                .Options;
            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            await using var db = new PlatformDbContext(options);
            var hash = new byte[] { 0xAF, 0x04, 0x00, 0x01 };
            var sql = db.StaffAccessTokens.Where(token => token.TokenHash == hash).ToQueryString();
            Assert.Contains("\"TokenHash\" = ", sql, StringComparison.Ordinal);

            // Запрос сервиса выполняется на PostgreSQL целиком: неизвестный токен — просто null, без исключения.
            Assert.Null(await new OpaqueStaffTokenService(db, TimeProvider.System).ValidateAsync("not-a-real-token", CancellationToken.None));
            Assert.Empty(await db.StaffAccessTokens.Where(token => token.TokenHash == hash && token.RevokedAtUtc == null).ToListAsync());
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
