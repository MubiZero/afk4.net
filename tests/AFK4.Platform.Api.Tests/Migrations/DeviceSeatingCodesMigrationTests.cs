namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>
/// Таблица кодов посадки на настоящей PostgreSQL: накатывается на чистую схему и откатывается
/// начисто. In-memory провайдер такую проверку не заменяет — он миграции не исполняет вовсе.
/// </summary>
public sealed class DeviceSeatingCodesMigrationTests
{
    private const string PreviousMigration = "AddBookingTruthfulness";
    private const string ThisMigration = "AddDeviceSeatingCodes";

    [MigrationPostgresFact]
    public async Task UpCreatesTheTable_AndDownRemovesIt()
    {
        await using var schema = await MigrationSchema.CreateAsync("seating_codes");

        await schema.MigrateToAsync(ThisMigration);
        Assert.Equal("1", await schema.ScalarAsync(TableExistsSql("device_seating_codes")));

        await schema.MigrateToAsync(PreviousMigration);
        Assert.Equal("0", await schema.ScalarAsync(TableExistsSql("device_seating_codes")));

        // Волна 2 при этом на месте: откат этой миграции не имеет права её задеть.
        Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql("reservations", "NoShowAtUtc")));
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
