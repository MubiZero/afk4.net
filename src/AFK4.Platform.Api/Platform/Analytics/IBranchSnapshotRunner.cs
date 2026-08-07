namespace AFK4.Platform.Api.Platform.Analytics;

public interface IBranchSnapshotRunner
{
    /// <summary>Дописывает недостающие суточные снимки филиалов вплоть до вчерашнего дня. Возвращает число записанных строк.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
