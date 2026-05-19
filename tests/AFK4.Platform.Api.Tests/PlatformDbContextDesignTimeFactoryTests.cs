using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class PlatformDbContextDesignTimeFactoryTests : IDisposable
{
    private readonly string? originalConnectionString;

    public PlatformDbContextDesignTimeFactoryTests()
    {
        originalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PlatformDatabase");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__PlatformDatabase", originalConnectionString);
    }

    [Fact]
    public void CreateDbContext_UsesPlatformDatabaseEnvironmentConnectionStringWhenPresent()
    {
        const string connectionString = "Host=localhost;Port=55432;Database=afk4_restore_rehearsal;Username=postgres;SSL Mode=Disable";
        Environment.SetEnvironmentVariable("ConnectionStrings__PlatformDatabase", connectionString);

        using var context = new PlatformDbContextDesignTimeFactory().CreateDbContext([]);

        Assert.Equal(connectionString, context.Database.GetConnectionString());
    }
}
