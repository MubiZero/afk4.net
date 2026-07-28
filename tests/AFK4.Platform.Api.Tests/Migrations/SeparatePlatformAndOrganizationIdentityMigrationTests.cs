using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Migrations;

public sealed class SeparatePlatformAndOrganizationIdentityMigrationTests
{
    [MigrationPostgresFact]
    public async Task PostgreSqlCutover_AbortsOnUnknownRoleThenMapsAtomically()
    {
        var connectionString = Environment.GetEnvironmentVariable(MigrationPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"identity_cutover_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(rootBuilder.ConnectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(rootBuilder.ConnectionString) { SearchPath = schema };
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, SchemaSql + SeedSql);
            var migrationSql = GetMigrationSql();

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, migrationSql, transaction));
                await transaction.RollbackAsync();
            }
            Assert.Equal("owner", await ScalarAsync(connection,
                "SELECT \"RoleName\" FROM staff_role_assignments WHERE \"RoleName\" = 'owner'"));

            await ExecuteAsync(connection, "DELETE FROM staff_role_assignments WHERE \"RoleName\" = 'mystery_role';");
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await ExecuteAsync(connection, migrationSql, transaction);
                await transaction.CommitAsync();
            }

            Assert.Equal("0", await ScalarAsync(connection, UnknownRolesSql));
            Assert.Equal("organization_owner,operator,accountant", await ScalarAsync(connection,
                "SELECT \"RoleNamesCsv\" FROM staff_invites"));
            Assert.Equal("[\"platform_admin\", \"platform_support\"]", await ScalarAsync(connection,
                "SELECT \"RolesJson\"::text FROM platform_admin_users"));
            Assert.Equal("0", await ScalarAsync(connection, ActiveTokensSql));
            Assert.Equal("0", await ScalarAsync(connection,
                "SELECT count(*) FROM device_credentials WHERE \"RevokedAtUtc\" IS NOT NULL"));
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public void Migration_ContainsGuardedRoleCutoverAndInteractiveTokenRevocation()
    {
        var migrationType = typeof(Program).Assembly.GetType(
            "AFK4.Platform.Api.Data.Migrations.SeparatePlatformAndOrganizationIdentity");
        Assert.NotNull(migrationType);

        var sql = GetMigrationSql();

        Assert.Contains("Unknown organization role", sql);
        Assert.Contains("Unknown platform role", sql);
        Assert.Contains("staff_role_assignments", sql);
        Assert.Contains("staff_invites", sql);
        Assert.Contains("staff_money_caps", sql);
        Assert.Contains("platform_admin_users", sql);
        Assert.Contains("organization_owner", sql);
        Assert.Contains("platform_admin", sql);
        Assert.Contains("staff_access_tokens", sql);
        Assert.Contains("staff_refresh_tokens", sql);
        Assert.Contains("platform_admin_access_tokens", sql);
        Assert.Contains("platform_admin_refresh_tokens", sql);
        Assert.Contains("player_access_tokens", sql);
        Assert.Contains("player_refresh_tokens", sql);
        Assert.DoesNotContain("device_credentials\" SET \"RevokedAtUtc", sql);
    }

    [Fact]
    public void Down_IsExplicitlyUnsupported()
    {
        var migrationType = typeof(Program).Assembly.GetType(
            "AFK4.Platform.Api.Data.Migrations.SeparatePlatformAndOrganizationIdentity");
        Assert.NotNull(migrationType);
        var migration = Assert.IsAssignableFrom<Migration>(Activator.CreateInstance(migrationType));
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        var error = Assert.Throws<TargetInvocationException>(() =>
            migrationType.GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(migration, [builder]));

        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    private static string GetMigrationSql()
    {
        var migrationType = typeof(Program).Assembly.GetType(
            "AFK4.Platform.Api.Data.Migrations.SeparatePlatformAndOrganizationIdentity")!;
        var migration = Assert.IsAssignableFrom<Migration>(Activator.CreateInstance(migrationType));
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migrationType.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return string.Join("\n", builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)!;
    }

    private const string SchemaSql = """
        CREATE TABLE staff_role_assignments ("StaffUserId" uuid, "OrganizationId" uuid, "BranchId" uuid, "RoleName" text);
        CREATE TABLE staff_money_caps ("BranchId" uuid, "ActionScope" text, "RoleName" text);
        CREATE TABLE staff_invites ("RoleNamesCsv" text);
        CREATE TABLE platform_admin_users ("RolesJson" jsonb);
        CREATE TABLE staff_access_tokens ("RevokedAtUtc" timestamptz);
        CREATE TABLE staff_refresh_tokens ("RevokedAtUtc" timestamptz);
        CREATE TABLE platform_admin_access_tokens ("RevokedAtUtc" timestamptz);
        CREATE TABLE platform_admin_refresh_tokens ("RevokedAtUtc" timestamptz);
        CREATE TABLE player_access_tokens ("RevokedAtUtc" timestamptz);
        CREATE TABLE player_refresh_tokens ("RevokedAtUtc" timestamptz);
        CREATE TABLE device_credentials ("RevokedAtUtc" timestamptz);
        """;

    private const string SeedSql = """
        INSERT INTO staff_role_assignments VALUES
          (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'owner'),
          (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'cashier_operator'),
          (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'accountant_auditor'),
          (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'mystery_role');
        INSERT INTO staff_money_caps VALUES (gen_random_uuid(), 'high_risk', 'cashier_operator');
        INSERT INTO staff_invites VALUES ('owner,cashier_operator,accountant_auditor');
        INSERT INTO platform_admin_users VALUES ('["platform_owner","platform_support"]');
        INSERT INTO staff_access_tokens VALUES (NULL);
        INSERT INTO staff_refresh_tokens VALUES (NULL);
        INSERT INTO platform_admin_access_tokens VALUES (NULL);
        INSERT INTO platform_admin_refresh_tokens VALUES (NULL);
        INSERT INTO player_access_tokens VALUES (NULL);
        INSERT INTO player_refresh_tokens VALUES (NULL);
        INSERT INTO device_credentials VALUES (NULL);
        """;

    private const string UnknownRolesSql = """
        SELECT count(*) FROM (
          SELECT "RoleName" role_name FROM staff_role_assignments
          UNION ALL SELECT "RoleName" FROM staff_money_caps
          UNION ALL SELECT btrim(role_name) FROM staff_invites, LATERAL regexp_split_to_table("RoleNamesCsv", ',') role_name
        ) roles WHERE role_name NOT IN ('organization_owner','branch_manager','shift_supervisor','operator','technician','accountant')
        """;

    private const string ActiveTokensSql = """
        SELECT
          (SELECT count(*) FROM staff_access_tokens WHERE "RevokedAtUtc" IS NULL) +
          (SELECT count(*) FROM staff_refresh_tokens WHERE "RevokedAtUtc" IS NULL) +
          (SELECT count(*) FROM platform_admin_access_tokens WHERE "RevokedAtUtc" IS NULL) +
          (SELECT count(*) FROM platform_admin_refresh_tokens WHERE "RevokedAtUtc" IS NULL) +
          (SELECT count(*) FROM player_access_tokens WHERE "RevokedAtUtc" IS NULL) +
          (SELECT count(*) FROM player_refresh_tokens WHERE "RevokedAtUtc" IS NULL)
        """;
}

public sealed class MigrationPostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING";

    public MigrationPostgresFactAttribute()
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(connectionString)
                || new NpgsqlConnectionStringBuilder(connectionString).Database?
                    .EndsWith("_test", StringComparison.OrdinalIgnoreCase) != true)
                Skip = $"Set {EnvironmentVariable} to a PostgreSQL database whose name ends with _test.";
        }
        catch (ArgumentException)
        {
            Skip = $"Set {EnvironmentVariable} to a valid PostgreSQL test connection string.";
        }
    }
}
