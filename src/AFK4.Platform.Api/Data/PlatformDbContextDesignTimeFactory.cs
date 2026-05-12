using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AFK4.Platform.Api.Data;

public sealed class PlatformDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=afk4_dev;Username=postgres")
            .Options;

        return new PlatformDbContext(options);
    }
}
