using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformRoleConcurrencyPostgresTests
{
    // Проверка «имя занято» в CreateCoreAsync — чтение перед записью: два одновременных создания
    // одной роли оба его проходят, и настоящий инвариант держит первичный ключ таблицы. На
    // InMemory-провайдере первичных ключей в этом смысле нет вовсе и столкновения не бывает —
    // притворяться, что этот сценарий там проверен, нельзя, поэтому тест идёт только на настоящем
    // Postgres. Проигравшая сторона обязана получить понятный role_name_taken, а не сырой 500.
    [PlatformAdminPostgresFact]
    public async Task ConcurrentCreate_OfTheSameRoleName_LeavesExactlyOneRoleAndAClearRefusal()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"platform_role_race_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(rootBuilder.ConnectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(scopedBuilder.ConnectionString)
                .Options;

            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            var now = DateTimeOffset.Parse("2026-08-09T08:00:00Z");
            var actorId = Guid.NewGuid();
            await using (var seedDb = new PlatformDbContext(options))
            {
                // Актор с полным доступом: иначе отказ придёт по «нельзя выдать больше, чем есть
                // у тебя», и гонка за именем роли вообще не начнётся.
                seedDb.PlatformRoles.Add(new PlatformRoleEntity
                {
                    RoleName = PlatformAdminRoleNames.PlatformAdmin,
                    DisplayName = "Администратор",
                    Description = string.Empty,
                    IsBuiltIn = true,
                    GrantsAllPermissions = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                seedDb.PlatformAdminUsers.Add(new PlatformAdminUserEntity
                {
                    PlatformAdminUserId = actorId,
                    UserName = "actor@platform.test",
                    NormalizedUserName = "ACTOR@PLATFORM.TEST",
                    DisplayName = "Актор",
                    PasswordHash = "x",
                    RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([PlatformAdminRoleNames.PlatformAdmin]),
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                await seedDb.SaveChangesAsync();
            }

            var request = new CreatePlatformRoleRequest(
                "race_role", "Гонка", "", [PlatformAdminPermissionNames.ViewOrganizations]);

            await using var dbLeft = new PlatformDbContext(options);
            await using var dbRight = new PlatformDbContext(options);
            var serviceLeft = new PlatformRoleService(
                dbLeft, new FixedTimeProvider(now), new EfPlatformRolePermissionResolver(dbLeft));
            var serviceRight = new PlatformRoleService(
                dbRight, new FixedTimeProvider(now), new EfPlatformRolePermissionResolver(dbRight));

            var results = await Task.WhenAll(
                serviceLeft.CreateAsync(actorId, request, CancellationToken.None),
                serviceRight.CreateAsync(actorId, request, CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Single(results, result => result.Succeeded);
            var loser = Assert.Single(results, result => !result.Succeeded);
            // Отказ проигравшего может прийти двумя честными путями: уникальность имени поймал
            // первичный ключ (role_name_taken) либо serializable отменил транзакцию целиком
            // (conflict, «повторите»). Оба объясняют причину правдиво; сырого 500 быть не должно.
            Assert.Contains(loser.ErrorCode, new[] { PlatformRoleErrorCodes.RoleNameTaken, PlatformRoleErrorCodes.Conflict });

            await using var verifyDb = new PlatformDbContext(options);
            Assert.Equal(1, await verifyDb.PlatformRoles.CountAsync(role => role.RoleName == "race_role"));
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
