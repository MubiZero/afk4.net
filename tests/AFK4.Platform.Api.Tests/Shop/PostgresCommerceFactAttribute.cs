using Npgsql;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class PostgresCommerceFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "AFK4_COMMERCE_TEST_POSTGRES";

    public PostgresCommerceFactAttribute()
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
