using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Platform.Entitlements;

public sealed class FeatureCatalogSeedHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<FeatureCatalogSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Добавляет только недостающие объявленные ключи и никогда не трогает существующую строку:
        // после создания авторитетна панель, и сидер, переписывающий имя/описание/умолчание на
        // каждом рестарте, молча откатывал бы осознанную правку. Раннего выхода «каталог непустой»
        // тоже нет: база из продакшена должна получать новые фичи следующим деплоем.
        var existingKeys = await dbContext.PlatformFeatures
            .Select(feature => feature.FeatureKey)
            .ToHashSetAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var added = 0;
        foreach (var declaration in FeatureCatalog.Declared)
        {
            if (existingKeys.Contains(declaration.FeatureKey))
            {
                continue;
            }

            dbContext.PlatformFeatures.Add(new PlatformFeatureEntity
            {
                FeatureKey = declaration.FeatureKey,
                Name = declaration.Name,
                Description = declaration.Description,
                EnabledByDefault = declaration.EnabledByDefault,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            added++;
        }

        if (added > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Feature catalog seed: added {Added} missing declared features.", added);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
