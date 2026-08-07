using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Platform.Identity;
using Microsoft.Extensions.Hosting;

namespace AFK4.Platform.Api.Tests.Architecture;

public sealed class PeriodicJobRegistrationTests
{
    // Одноразовые сервисы старта: они не тикают, записывать им нечего.
    private static readonly HashSet<string> OneShotStartupServices = new(StringComparer.Ordinal)
    {
        "PlatformAdminBootstrapHostedService",
        "BillingPlanSeedHostedService"
    };

    // Смысл теста: наблюдение за фоновыми заданиями не должно держаться на памяти автора
    // седьмого задания. Новый BackgroundService, унаследованный напрямую, назовут поимённо здесь.
    [Fact]
    public void EveryPeriodicBackgroundService_DerivesFromPlatformPeriodicJob()
    {
        var offenders = typeof(PlatformAdminBootstrapHostedService).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .Where(type => typeof(BackgroundService).IsAssignableFrom(type))
            .Where(type => !typeof(PlatformPeriodicJob).IsAssignableFrom(type))
            .Where(type => !OneShotStartupServices.Contains(type.Name))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }
}
