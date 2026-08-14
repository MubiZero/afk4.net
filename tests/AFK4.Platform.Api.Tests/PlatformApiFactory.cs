using System.Net;
using System.Security.Cryptography;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Media;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Tests.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Tests;

// Building a WebApplicationFactory<Program> host costs ~650ms, and the suite used to build one per
// test (~450 times). This wrapper keeps the old `new PlatformApiFactory()` / `.CreateClient()` /
// `.Services` surface but routes the common case through ONE shared host. Per-test isolation comes
// from a unique InMemory database name carried in an AsyncLocal: the DbContext options are resolved
// per-scope and read the ambient name, and TestServer.PreserveExecutionContext lets that ambient
// value flow into request handling. Tests that customise the host (real session billing or
// extraServices) still get their own throwaway host — there are only a handful of those.
internal sealed class PlatformApiFactory : IAsyncDisposable, IDisposable
{
    private static readonly AsyncLocal<string?> CurrentDatabaseName = new();
    private static readonly AsyncLocal<IPAddress?> CurrentClientIp = new();
    private static readonly AsyncLocal<SharedAppHost?> CurrentHost = new();
    private static readonly SharedAppHost DefaultHost = new(useRealSessionBilling: false, extraServices: null);
    private static int clientIpCounter;

    private readonly string databaseName = Guid.NewGuid().ToString("N");
    private readonly IPAddress clientIp = NextTestClientIp();
    private readonly SharedAppHost host;
    private readonly bool ownsHost;
    private int seeded;

    // A unique private-range (10.x.x.x) IP per test. Unique so the IP-partitioned rate limiter and
    // install throttle isolate per test on the shared host; private-range so the app still trusts the
    // X-Forwarded-For header exactly as it did when TestServer left RemoteIpAddress null.
    private static IPAddress NextTestClientIp()
    {
        var n = Interlocked.Increment(ref clientIpCounter);
        return new IPAddress([10, (byte)(n >> 16), (byte)(n >> 8), (byte)n]);
    }

    public PlatformApiFactory(bool useRealSessionBilling = false, Action<IServiceCollection>? extraServices = null)
    {
        if (!useRealSessionBilling && extraServices is null)
        {
            host = DefaultHost;
            ownsHost = false;
        }
        else
        {
            host = new SharedAppHost(useRealSessionBilling, extraServices);
            ownsHost = true;
        }
    }

    public IServiceProvider Services
    {
        get
        {
            Activate();
            return host.Services;
        }
    }

    public HttpClient CreateClient()
    {
        Activate();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            OrganizationAdminCompatibilityMiddleware.ProductHeader,
            OrganizationAdminCompatibilityMiddleware.ProductName);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            OrganizationAdminCompatibilityMiddleware.EpochHeader,
            "2");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            OrganizationAdminCompatibilityMiddleware.VersionHeader,
            "test");
        return client;
    }

    // Make this test's database the ambient one and seed it once. Called from the test's own async
    // context, so the AsyncLocal value flows into both direct seeding scopes and TestServer requests.
    private void Activate()
    {
        CurrentDatabaseName.Value = databaseName;
        CurrentClientIp.Value = clientIp;
        CurrentHost.Value = host;
        host.EnsureStarted();
        if (Interlocked.Exchange(ref seeded, 1) == 0)
        {
            host.SeedBaseline();
        }
    }

    // Lets test helpers that only receive an HttpClient (because they mirror an HTTP route under
    // test, e.g. TwoFactorTestHelper) still reach the database of whichever PlatformApiFactory is
    // active in the calling test's async context — no separate `factory` parameter to thread
    // through. Valid only while called from within that test's own async call chain (same rule the
    // per-test database/IP AsyncLocals above already rely on).
    internal static IServiceProvider CurrentAmbientServices =>
        CurrentHost.Value?.Services
        ?? throw new InvalidOperationException(
            "No PlatformApiFactory is active in the current async context — call factory.CreateClient() first.");

    public async ValueTask DisposeAsync()
    {
        if (ownsHost)
        {
            await host.DisposeAsync();
        }
    }

    public void Dispose()
    {
        if (ownsHost)
        {
            host.Dispose();
        }
    }

    private sealed class SharedAppHost(bool useRealSessionBilling, Action<IServiceCollection>? extraServices)
        : WebApplicationFactory<Program>
    {
        private readonly object startGate = new();
        private bool started;

        public void EnsureStarted()
        {
            if (started)
            {
                return;
            }

            lock (startGate)
            {
                if (started)
                {
                    return;
                }

                _ = Services;                          // force the host to build
                Server.PreserveExecutionContext = true; // let the test's AsyncLocal reach request handling
                started = true;
            }
        }

        // Reproduce the baseline data the BillingPlanSeed hosted service would have seeded on startup.
        // Hosted services are removed from the test host (timers add noise on a shared host), so we run
        // the real seeder against each fresh per-test database instead of duplicating the plan data.
        public void SeedBaseline()
        {
            var seeder = new BillingPlanSeedHostedService(
                Services,
                Services.GetRequiredService<TimeProvider>(),
                Services.GetRequiredService<ILogger<BillingPlanSeedHostedService>>());
            seeder.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            var featureSeeder = new FeatureCatalogSeedHostedService(
                Services,
                Services.GetRequiredService<TimeProvider>(),
                Services.GetRequiredService<ILogger<FeatureCatalogSeedHostedService>>());
            featureSeeder.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            var roleSeeder = new PlatformRoleSeedHostedService(
                Services,
                Services.GetRequiredService<TimeProvider>(),
                Services.GetRequiredService<ILogger<PlatformRoleSeedHostedService>>());
            roleSeeder.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // The app logs at Information (every EF SQL). On a shared host the timer-driven hosted
            // services would log against a stale database forever, so silence providers to Warning.
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Warning);
            });

            builder.ConfigureServices(services =>
            {
                // Шлюз SMS принимает только одобренные шаблоны, поэтому канал молчит, пока их
                // идентификаторы не заданы. Тестам нужны те же настройки, что и проду, иначе
                // проверки кода подтверждения ловили бы отсутствие конфигурации, а не поведение.
                services.Configure<AFK4.Platform.Api.Notifications.SmsOptions>(options =>
                {
                    options.ApiToken = "test-token";
                    foreach (var key in new[]
                             {
                                 AFK4.Platform.Api.Notifications.NotificationTemplateKeys.PlayerSignInCode,
                                 AFK4.Platform.Api.Notifications.NotificationTemplateKeys.PlayerPhoneVerification,
                                 AFK4.Platform.Api.Notifications.NotificationTemplateKeys.StaffPhoneVerification,
                                 AFK4.Platform.Api.Notifications.NotificationTemplateKeys.StaffPasswordResetSms,
                             })
                    {
                        options.TemplateIds[key] = "test-code-template";
                    }
                });
                services.RemoveAll<IDbContextOptionsConfiguration<PlatformDbContext>>();
                services.RemoveAll<DbContextOptions<PlatformDbContext>>();
                services.RemoveAll<PlatformDbContext>();
                // Scoped options so the InMemory database name is re-read from the AsyncLocal on every
                // scope (each request and each direct seeding scope), giving per-test isolation.
                services.AddDbContext<PlatformDbContext>(
                    (_, options) => options
                        .UseInMemoryDatabase(CurrentDatabaseName.Value ?? "uninitialized")
                        // Several services (billing, POS, inventory, owner-transfer, ...) wrap multi-step
                        // writes in an explicit transaction for atomicity. The in-memory provider doesn't
                        // support real transactions but happily no-ops them; without this the no-op itself
                        // throws instead of just being logged.
                        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)),
                    contextLifetime: ServiceLifetime.Scoped,
                    optionsLifetime: ServiceLifetime.Scoped);

                // Timer/seed hosted services would run once on the shared host against one database;
                // drop them and reproduce the needed seed per-test (SeedBaseline). Tests that exercise
                // a hosted service invoke it directly.
                services.RemoveAll<IHostedService>();

                // The rate limiter and install throttle partition by client IP, which is empty for all
                // TestServer requests — on a shared host every test would land in one partition and trip
                // the limit. Give each test its own synthetic IP (see NextTestClientIp) so the per-test
                // isolation the limiter assumes is restored, including the test that asserts 429.
                services.AddTransient<IStartupFilter, PerTestClientIpStartupFilter>();

                if (!useRealSessionBilling)
                {
                    services.RemoveAll<ISessionBillingService>();
                    services.AddSingleton<ISessionBillingService, FakeSessionBillingService>();
                }

                // Prod registration of IMediaStorage is Singleton; the fake must be too, so a test can
                // resolve the SAME instance the endpoint used (see FakeMediaStorage.Objects).
                services.RemoveAll<IMediaStorage>();
                services.AddSingleton<IMediaStorage, FakeMediaStorage>();

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

        // Runs first in the pipeline (before UseRateLimiter) and stamps a per-test client IP.
        private sealed class PerTestClientIpStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.Use(async (context, advance) =>
                {
                    context.Connection.RemoteIpAddress = CurrentClientIp.Value ?? IPAddress.Loopback;
                    await advance();
                });
                next(app);
            };
        }
    }
}
