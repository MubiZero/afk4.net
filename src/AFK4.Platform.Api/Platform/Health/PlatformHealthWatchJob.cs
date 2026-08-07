using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
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
    PlatformJobIntervalCatalog jobIntervalCatalog,
    ILogger<PlatformHealthWatchJob> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly PlatformHealthOptions healthOptions = healthOptions.Value;
    private DateTimeOffset lastPruneAtUtc = DateTimeOffset.MinValue;

    protected override string JobName => PlatformJobNames.HealthWatch;

    protected override TimeSpan Interval => healthOptions.WatchInterval;

    // Словарь покрывает ВЕСЬ PlatformJobNames.Watched — источник единственный, PlatformJobIntervalCatalog,
    // общий со службой обзора здоровья. Задание, забытое там, молча выпадает из наблюдения ОБОИХ
    // потребителей сразу — ровно та дыра, которую закрывает этот план. internal (не private), чтобы
    // тест на полноту мог проверить содержимое напрямую, а не через наблюдаемый побочный эффект.
    internal IReadOnlyDictionary<string, TimeSpan> JobIntervals => jobIntervalCatalog.Build();

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

    /// <summary>
    /// Запас сверх самого широкого окна просрочки: без него серия провалов задания с интервалом
    /// у самой границы окна теряла бы старые попытки из выборки и недосчитывала бы серию.
    /// </summary>
    private static readonly TimeSpan RunHistoryMargin = TimeSpan.FromHours(6);

    private async Task<HealthSnapshot> BuildSnapshotAsync(PlatformDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var jobIntervals = JobIntervals;

        // Глубина выборки производна от окон просрочки наблюдаемых заданий (те же правила,
        // что применяет PlatformHealthRules), а не зашитая константа — иначе задание с интервалом
        // больше жёстко заданной глубины молча выпадало бы из-под наблюдения при смене конфигурации.
        var maxOverdueWindow = jobIntervals.Values.Select(PlatformHealthRules.OverdueWindow).Max();
        var since = now - maxOverdueWindow - RunHistoryMargin;
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
        foreach (var (jobName, interval) in jobIntervals)
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

        // Retention — обслуживание, а не детектирование. Собственный try/catch не даёт постороннему
        // сбою чистки (обрыв соединения, взаимоблокировка) пометить успешно отработавший тик как
        // Failed: три таких тика подряд завели бы job_failing про сам сторож — ложный сигнал
        // ровно там, где системе нужна достоверность.
        try
        {
            var cutoff = now - healthOptions.JobRunRetention;
            await db.PlatformJobRuns.Where(run => run.StartedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Log.LogError(exception, "Failed to prune old rows of {JobName} runs.", JobName);
        }
    }
}
