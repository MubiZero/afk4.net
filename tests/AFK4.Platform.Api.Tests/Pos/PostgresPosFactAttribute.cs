using Npgsql;

namespace AFK4.Platform.Api.Tests.Pos;

public sealed class PostgresPosFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "AFK4_POS_POSTGRES_TEST_CONNECTION_STRING";

    public PostgresPosFactAttribute()
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
