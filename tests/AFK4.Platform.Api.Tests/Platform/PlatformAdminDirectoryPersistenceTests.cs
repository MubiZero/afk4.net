using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminDirectoryPersistenceTests
{
    [Fact]
    public async Task Invitation_RoundTripsAndCodeHashIsUnique()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hash = new byte[] { 1, 2, 3, 4 };

        db.PlatformAdminInvitations.Add(new PlatformAdminInvitationEntity
        {
            InvitationId = Guid.NewGuid(),
            CodeHash = hash,
            Role = PlatformAdminRoleNames.PlatformSupport,
            Status = "pending",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(3),
            CreatedByPlatformAdminUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var stored = await db.PlatformAdminInvitations.SingleAsync();
        Assert.Equal("pending", stored.Status);
        Assert.Equal(hash, stored.CodeHash);
    }

    [Fact]
    public async Task AdminUser_HasTwoFactorColumnsWithSafeDefaults()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var user = await db.PlatformAdminUsers.FirstAsync();

        Assert.Null(user.TotpSecretEncrypted);
        Assert.Null(user.TotpEnabledAtUtc);
        Assert.Equal("[]", user.RecoveryCodeHashesJson);
        Assert.Equal(0, user.FailedTwoFactorAttempts);
    }

    // The InMemory provider (used above) always applies the C# property initializer
    // (`= "[]"`) for a row EF itself inserts — it would pass even if the migration's SQL
    // default were wrong. Existing rows created before this migration (or by raw SQL that
    // doesn't set the column) only get a value from the database's DEFAULT clause, which is
    // driven by `HasDefaultValue` in OnModelCreating, not the CLR initializer. Prove that by
    // inserting a row against real PostgreSQL with the new columns omitted entirely, then
    // reading it back through EF.
    [PlatformAdminPostgresFact]
    public async Task AdminUser_RecoveryCodeHashesJson_UsesSqlDefaultForRowsMissingTheColumn()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"platform_admin_directory_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(builder.ConnectionString);
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

            var userId = Guid.NewGuid();
            await using (var connection = new NpgsqlConnection(scopedBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var insert = connection.CreateCommand();
                // Deliberately omits RecoveryCodeHashesJson and FailedTwoFactorAttempts so
                // Postgres has to fill them in from the column DEFAULT, exactly like an
                // existing pre-migration row would after the ALTER TABLE runs.
                insert.CommandText = """
                    INSERT INTO platform_admin_users
                        ("PlatformAdminUserId", "UserName", "NormalizedUserName", "DisplayName",
                         "PasswordHash", "RolesJson", "IsActive", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES
                        (@id, 'sql-default@platform.test', 'SQL-DEFAULT@PLATFORM.TEST', 'SQL Default Admin',
                         'irrelevant-hash', '[]', true, now(), now())
                    """;
                insert.Parameters.AddWithValue("id", userId);
                await insert.ExecuteNonQueryAsync();
            }

            await using var db = new PlatformDbContext(options);
            var stored = await db.PlatformAdminUsers.SingleAsync(admin => admin.PlatformAdminUserId == userId);

            Assert.Equal("[]", stored.RecoveryCodeHashesJson);
            Assert.Equal(0, stored.FailedTwoFactorAttempts);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }
}

public sealed class PlatformAdminPostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING";

    public PlatformAdminPostgresFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString) || !IsTestDatabase(connectionString))
        {
            Skip = $"Set {EnvironmentVariable} to a PostgreSQL database whose name ends with _test.";
        }
    }

    private static bool IsTestDatabase(string connectionString)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Database?
                .EndsWith("_test", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
