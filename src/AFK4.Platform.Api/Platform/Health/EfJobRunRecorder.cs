using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

public sealed class EfJobRunRecorder(PlatformDbContext dbContext) : IJobRunRecorder
{
    // Колонка Error — 2000 символов; исключение с длинным стеком иначе даст 22001 и уронит
    // сам регистратор, то есть авария съела бы запись о себе.
    private const int MaxErrorLength = 2000;

    public async Task RecordAsync(
        string jobName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        string outcome,
        int itemsProcessed,
        string? error,
        CancellationToken cancellationToken)
    {
        dbContext.PlatformJobRuns.Add(new PlatformJobRunEntity
        {
            PlatformJobRunId = Guid.NewGuid(),
            JobName = jobName,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = finishedAtUtc,
            Outcome = outcome,
            ItemsProcessed = itemsProcessed,
            Error = error is null ? null : error[..Math.Min(error.Length, MaxErrorLength)]
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
