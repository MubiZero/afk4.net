namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Привязка токенов игрока к ПК и счёт неудачных входов на машине — на настоящей PostgreSQL:
/// накатывается на схему с данными и откатывается начисто.
/// </summary>
public sealed class DevicePlayerSignInMigrationTests
{
    private const string PreviousMigration = "DropPlayerDeviceLocale";
    private const string ThisMigration = "AddDevicePlayerSignIn";

    [MigrationPostgresFact]
    public async Task UpAddsTheColumns_AndDownRemovesThem()
    {
        await using var schema = await MigrationSchema.CreateAsync("device_sign_in");

        await schema.MigrateToAsync(ThisMigration);
        foreach (var (table, column) in Columns)
        {
            Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql(table, column)));
        }

        // Счёт неудач у уже заведённых машин начинается с нуля, а не с NULL: иначе первая же
        // неудача на старом ПК упала бы на сложении.
        Assert.Equal("0", await schema.ScalarAsync("""
            SELECT column_default FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'devices'
              AND column_name = 'PlayerSignInFailedCount'
            """));

        await schema.MigrateToAsync(PreviousMigration);
        foreach (var (table, column) in Columns)
        {
            Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql(table, column)));
        }
    }

    private static readonly (string Table, string Column)[] Columns =
    [
        ("platform_person_access_tokens", "DeviceId"),
        ("platform_person_access_tokens", "DeviceSignedInAtUtc"),
        ("platform_person_refresh_tokens", "DeviceId"),
        ("platform_person_refresh_tokens", "DeviceSignedInAtUtc"),
        ("devices", "PlayerSignInFailedCount"),
        ("devices", "PlayerSignInWindowStartedAtUtc")
    ];

    private static string ColumnExistsSql(string table, string column) => $"""
        SELECT count(*)::text FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = '{table}' AND column_name = '{column}'
        """;
}
