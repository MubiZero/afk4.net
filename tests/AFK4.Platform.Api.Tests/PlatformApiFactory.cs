using System.Security.Cryptography;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Platform.Identity;
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
    private readonly bool useRealSessionBilling;

    public PlatformApiFactory(bool useRealSessionBilling = false)
    {
        this.useRealSessionBilling = useRealSessionBilling;
    }

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
            if (!useRealSessionBilling)
            {
                services.RemoveAll<ISessionBillingService>();
                services.AddSingleton<ISessionBillingService, FakeSessionBillingService>();
            }

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var signingPrivateKeyPem = key.ExportECPrivateKeyPem();
            services.Configure<SessionLeaseOptions>(options =>
            {
                options.SigningPrivateKeyPem = signingPrivateKeyPem;
            });
            services.PostConfigure<InstallOptions>(options =>
            {
                options.LeaseSigningPublicKeyPem = "test-lease-public-key";
                options.UpdatePackageSigningPublicKeyPem = "test-update-public-key";
            });

            // Suppress configuration-driven platform admin bootstrap during tests so each test
            // controls its own admin seeding. Tests that exercise bootstrap should call the
            // hosted service directly with explicit options.
            services.PostConfigure<PlatformAdminBootstrapOptions>(options =>
            {
                options.UserName = null;
                options.Password = null;
                options.DisplayName = null;
                options.Roles = null;
            });
        });
    }
}
