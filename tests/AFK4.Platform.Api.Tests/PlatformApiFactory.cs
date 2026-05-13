using System.Security.Cryptography;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Sessions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests;

internal sealed class PlatformApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<PlatformDbContext>>();
            services.RemoveAll<DbContextOptions<PlatformDbContext>>();
            services.RemoveAll<PlatformDbContext>();
            services.AddDbContext<PlatformDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
            });

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var signingPrivateKeyPem = key.ExportECPrivateKeyPem();
            services.Configure<SessionLeaseOptions>(options =>
            {
                options.SigningPrivateKeyPem = signingPrivateKeyPem;
            });
        });
    }
}
