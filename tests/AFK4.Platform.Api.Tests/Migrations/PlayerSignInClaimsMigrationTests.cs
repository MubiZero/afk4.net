namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Заявки на вход по QR и счётчики неверных кодов посадки — на настоящей PostgreSQL: таблицы
/// накатываются и откатываются начисто, соседняя миграция входа на ПК остаётся на месте.
/// </summary>
public sealed class PlayerSignInClaimsMigrationTests
{
    private const string PreviousMigration = "AddDevicePlayerSignIn";
    private const string ThisMigration = "AddPlayerSignInClaims";

    [MigrationPostgresFact]
    public async Task UpCreatesTheTables_AndDownRemovesThem()
    {
        await using var schema = await MigrationSchema.CreateAsync("sign_in_claims");

        await schema.MigrateToAsync(ThisMigration);
        Assert.Equal("1", await schema.ScalarAsync(TableExistsSql("player_sign_in_claims")));
        Assert.Equal("1", await schema.ScalarAsync(TableExistsSql("seating_code_attempt_counters")));

        await schema.MigrateToAsync(PreviousMigration);
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("player_sign_in_claims")));
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("seating_code_attempt_counters")));
        Assert.Equal("1", await schema.ScalarAsync("""
            SELECT count(*)::text FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'platform_person_refresh_tokens'
              AND column_name = 'DeviceId'
            """));
    }

    private static string TableExistsSql(string table) => $"""
        SELECT count(*)::text FROM information_schema.tables
        WHERE table_schema = current_schema() AND table_name = '{table}'
        """;
}
