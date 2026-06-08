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
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Tests;

internal sealed class PlatformApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = Guid.NewGuid().ToString("N");
    private readonly bool useRealSessionBilling;
    private readonly Action<IServiceCollection>? extraServices;

    public PlatformApiFactory(bool useRealSessionBilling = false, Action<IServiceCollection>? extraServices = null)
    {
        this.useRealSessionBilling = useRealSessionBilling;
        this.extraServices = extraServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The app logs at Information by default (every EF SQL command included). Written to the
        // Console/Debug providers — both process-wide synchronized — that logging serializes the
        // hundreds of parallel factory-backed tests on a single lock. Tests assert on behaviour,
        // not log output, so silence the providers and raise the floor to Warning.
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

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

            services.PostConfigure<AFK4.Platform.Api.Security.SecretProtectionOptions>(options =>
            {
                // Throwaway 32-byte (all-zero) key, base64. Tests only.
                options.EncryptionKeyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
            });

            extraServices?.Invoke(services);
        });
    }
}
