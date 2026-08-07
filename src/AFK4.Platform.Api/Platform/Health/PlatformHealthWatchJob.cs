using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Применяет правила здоровья к записям прогонов и к двум очередям, заводит и закрывает инциденты
/// и вызывает оповещатель. Само задание тоже периодическое и потому видно на собственном экране;
/// за смертью процесса целиком следит внешняя проверка /api/health.
/// </summary>
public sealed class PlatformHealthWatchJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<PlatformHealthOptions> healthOptions,
    IOptions<BillingOptions> billingOptions,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<OutboxOptions> outboxOptions,
    AutoProtectionOptions autoProtectionOptions,
    ILogger<PlatformHealthWatchJob> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly PlatformHealthOptions healthOptions = healthOptions.Value;
    private DateTimeOffset lastPruneAtUtc = DateTimeOffset.MinValue;

    protected override string JobName => PlatformJobNames.HealthWatch;

    protected override TimeSpan Interval => healthOptions.WatchInterval;

    // Словарь покрывает ВЕСЬ PlatformJobNames.Watched. Задание, забытое здесь, молча выпадает
    // из наблюдения — ровно та дыра, которую закрывает этот план. internal (не private), чтобы
    // тест на полноту мог проверить содержимое напрямую, а не через наблюдаемый побочный эффект.
    internal IReadOnlyDictionary<string, TimeSpan> JobIntervals => new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
    {
        [PlatformJobNames.InvoiceGeneration] = billingOptions.Value.GenerationInterval,
        [PlatformJobNames.BillingOutbox] = outboxOptions.Value.PollInterval,
        [PlatformJobNames.NotificationDispatch] = notificationOptions.Value.PollInterval,
        [PlatformJobNames.DailySummary] = notificationOptions.Value.DailySummaryInterval,
        [PlatformJobNames.ScheduledReports] = notificationOptions.Value.ScheduledReportInterval,
        [PlatformJobNames.AutoProtection] = autoProtectionOptions.TickInterval,
        [PlatformJobNames.HealthWatch] = healthOptions.WatchInterval
    };

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var now = GetUtcNow();
        var db = scopedServices.GetRequiredService<PlatformDbContext>();
        var incidents = scopedServices.GetRequiredService<IPlatformIncidentService>();
        var notifier = scopedServices.GetRequiredService<IPlatformAlertNotifier>();

        var snapshot = await BuildSnapshotAsync(db, now, cancellationToken);
        var problems = PlatformHealthRules.Evaluate(snapshot, now);

        var opened = 0;
        foreach (var problem in problems)
        {
            var transition = await incidents.OpenOrTouchAsync(
                problem.Kind, problem.DedupKey, problem.Severity, problem.DetailsJson, cancellationToken);
            if (transition.IsNew || transition.ShouldRemind)
            {
                await notifier.NotifyOpenedAsync(transition.Incident, cancellationToken);
                opened++;
            }
        }

        // Первый аргумент — виды, которые этот проход РЕАЛЬНО проверял. Без него служба
        // закрывала бы и чужие открытые инциденты просто потому, что их ключей нет в наборе.
        var resolved = await incidents.ResolveMissingAsync(
            PlatformHealthRules.EvaluatedKinds,
            problems.Select(problem => problem.DedupKey).ToHashSet(StringComparer.Ordinal),
            cancellationToken);
        foreach (var incident in resolved)
        {
            await notifier.NotifyResolvedAsync(incident, cancellationToken);
        }

        await PruneJobRunsAsync(db, now, cancellationToken);
        return opened + resolved.Count;
    }

    private async Task<HealthSnapshot> BuildSnapshotAsync(PlatformDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now - TimeSpan.FromDays(2);
        // Один запрос на всю историю прогонов за окно; группировка в памяти — как в пульсе.
        var runs = await db.PlatformJobRuns
            .AsNoTracking()
            .Where(run => run.StartedAtUtc >= since)
            .Select(run => new { run.JobName, run.StartedAtUtc, run.Outcome })
            .ToListAsync(cancellationToken);

        var runsByJob = runs
            .GroupBy(run => run.JobName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(run => run.StartedAtUtc).ToList(), StringComparer.Ordinal);

        var jobs = new List<JobState>();
        foreach (var (jobName, interval) in JobIntervals)
        {
            runsByJob.TryGetValue(jobName, out var jobRuns);
            var lastSuccess = jobRuns?
                .FirstOrDefault(run => run.Outcome == PlatformJobOutcomeNames.Succeeded)?.StartedAtUtc;
            var streak = jobRuns is null ? 0 : jobRuns.TakeWhile(run => run.Outcome == PlatformJobOutcomeNames.Failed).Count();
            jobs.Add(new JobState(jobName, interval, lastSuccess, streak));
        }

        var stuckBefore = now - healthOptions.QueueStuckThreshold;
        var notificationFailed = await db.NotificationOutbox
            .CountAsync(row => row.Status == NotificationOutboxStatus.Failed, cancellationToken);
        var notificationStuck = await db.NotificationOutbox
            .CountAsync(row => row.Status == NotificationOutboxStatus.Pending && row.CreatedUtc < stuckBefore, cancellationToken);
        var outboxFailed = await db.OutboxMessages
            .CountAsync(row => row.Status == OutboxMessageStatus.Failed, cancellationToken);
        var outboxStuck = await db.OutboxMessages
            .CountAsync(row => row.Status == OutboxMessageStatus.Pending && row.CreatedAtUtc < stuckBefore, cancellationToken);

        return new HealthSnapshot(jobs, notificationFailed, notificationStuck, outboxFailed, outboxStuck);
    }

    private async Task PruneJobRunsAsync(PlatformDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (now - lastPruneAtUtc < TimeSpan.FromDays(1)) return;
        lastPruneAtUtc = now;

        var cutoff = now - healthOptions.JobRunRetention;
        await db.PlatformJobRuns.Where(run => run.StartedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
