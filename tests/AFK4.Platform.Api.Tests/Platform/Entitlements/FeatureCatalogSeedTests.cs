using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class FeatureCatalogSeedTests
{
    // The test host strips every IHostedService registration (see PlatformApiFactory.SharedAppHost)
    // to keep timer-driven jobs off the shared host, so it can't be resolved via
    // GetServices<IHostedService>() here — construct it directly instead, mirroring
    // BillingPlanSeedHostedServiceTests.
    private static FeatureCatalogSeedHostedService NewSeeder(IServiceProvider services) =>
        new(
            services,
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<FeatureCatalogSeedHostedService>>());

    [Fact]
    public async Task Seed_CreatesEveryDeclaredFeature_EnabledByDefault()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var features = await db.PlatformFeatures.AsNoTracking().ToListAsync();

        Assert.Equal(PlatformFeatureNames.All.Count, features.Count);
        Assert.All(PlatformFeatureNames.All, key => Assert.Contains(features, feature => feature.FeatureKey == key));
        // Сегодняшнее поведение не меняется: всё, что работало, продолжает работать у всех.
        Assert.All(features, feature => Assert.True(feature.EnabledByDefault));
    }

    [Fact]
    public async Task Seed_DoesNotOverwriteAnExistingRow()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var feature = await db.PlatformFeatures.SingleAsync(row => row.FeatureKey == PlatformFeatureNames.PlayerShop);
        feature.EnabledByDefault = false;
        feature.Description = "Отредактировано в панели";
        await db.SaveChangesAsync();

        var seeder = NewSeeder(scope.ServiceProvider);
        await seeder.StartAsync(CancellationToken.None);

        // Панель авторитетна после создания строки: повторный старт не откатывает осознанную правку.
        var reloaded = await db.PlatformFeatures.AsNoTracking()
            .SingleAsync(row => row.FeatureKey == PlatformFeatureNames.PlayerShop);
        Assert.False(reloaded.EnabledByDefault);
        Assert.Equal("Отредактировано в панели", reloaded.Description);
    }

    [Fact]
    public async Task Seed_AddsAMissingKnownFeature_WhenCatalogIsPartiallyFilled()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformFeatures.RemoveRange(
            await db.PlatformFeatures.Where(row => row.FeatureKey == PlatformFeatureNames.Loyalty).ToListAsync());
        await db.SaveChangesAsync();

        var seeder = NewSeeder(scope.ServiceProvider);
        await seeder.StartAsync(CancellationToken.None);

        // Непустой каталог — не повод выйти рано: база из продакшена должна получить новые фичи.
        Assert.True(await db.PlatformFeatures.AnyAsync(row => row.FeatureKey == PlatformFeatureNames.Loyalty));
    }
}
