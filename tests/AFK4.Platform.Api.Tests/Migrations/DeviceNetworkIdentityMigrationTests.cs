namespace AFK4.Platform.Api.Tests.Migrations;

/// <summary>Сетевой адрес ПК для пробуждения соседом — на настоящей PostgreSQL, туда и обратно.</summary>
public sealed class DeviceNetworkIdentityMigrationTests
{
    private const string PreviousMigration = "AddPlayerSignInClaims";
    private const string ThisMigration = "AddDeviceNetworkIdentity";

    [MigrationPostgresFact]
    public async Task UpAddsTheColumns_AndDownRemovesThem()
    {
        await using var schema = await MigrationSchema.CreateAsync("device_network");

        await schema.MigrateToAsync(ThisMigration);
        foreach (var column in Columns)
        {
            Assert.Equal("1", await schema.ScalarAsync(ColumnExistsSql(column)));
        }

        await schema.MigrateToAsync(PreviousMigration);
        foreach (var column in Columns)
        {
            Assert.Equal("0", await schema.ScalarAsync(ColumnExistsSql(column)));
        }
    }

    private static readonly string[] Columns = ["NetworkMacAddress", "NetworkSubnet", "NetworkBroadcastAddress"];

    private static string ColumnExistsSql(string column) => $"""
        SELECT count(*)::text FROM information_schema.columns
        WHERE table_schema = current_schema() AND table_name = 'devices' AND column_name = '{column}'
        """;
}
