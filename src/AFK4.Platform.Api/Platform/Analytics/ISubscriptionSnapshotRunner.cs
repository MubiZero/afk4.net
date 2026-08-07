namespace AFK4.Platform.Api.Platform.Analytics;

public interface ISubscriptionSnapshotRunner
{
    /// <summary>Дописывает недостающие суточные снимки вплоть до вчерашнего дня. Возвращает число записанных строк.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
