using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AFK4.Platform.Api.Data;

public sealed class PlatformDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=afk4_dev;Username=postgres";

    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PlatformDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PlatformDbContext(options);
    }
}
