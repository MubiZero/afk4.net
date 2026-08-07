namespace AFK4.Platform.Api.Platform.Health;

/// <summary>Шов записи прогонов: позволяет тестировать обёртку без базы.</summary>
public interface IJobRunRecorder
{
    Task RecordAsync(
        string jobName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        string outcome,
        int itemsProcessed,
        string? error,
        CancellationToken cancellationToken);
}
