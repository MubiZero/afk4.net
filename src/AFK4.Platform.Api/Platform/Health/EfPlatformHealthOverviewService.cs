using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Shared.Contracts.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Собирает тот же снимок, что и <see cref="PlatformHealthWatchJob"/> — читает, не оценивает
/// и не заводит инциденты. Интервалы заданий берёт из общего <see cref="PlatformJobIntervalCatalog"/>,
/// чтобы список наблюдаемых заданий и глубина выборки не разошлись со сторожем.
/// </summary>
public sealed class EfPlatformHealthOverviewService(
    PlatformDbContext dbContext,
    IPlatformIncidentService incidentService,
    PlatformJobIntervalCatalog jobIntervalCatalog,
    IOptions<PlatformHealthOptions> healthOptions,
    TimeProvider timeProvider)
    : IPlatformHealthOverviewService
{
    // Тот же запас, что у сторожа (см. PlatformHealthWatchJob.RunHistoryMargin) — без него серия
    // провалов задания с интервалом у самой границы окна теряла бы старые попытки из выборки.
    private static readonly TimeSpan RunHistoryMargin = TimeSpan.FromHours(6);

    public async Task<PlatformHealthOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var jobIntervals = jobIntervalCatalog.Build();

        var maxOverdueWindow = jobIntervals.Values.Select(PlatformHealthRules.OverdueWindow).Max();
        var since = now - maxOverdueWindow - RunHistoryMargin;

        // Один запрос на всю историю прогонов за окно; группировка в памяти — как в сторожe.
        var runs = await dbContext.PlatformJobRuns
            .AsNoTracking()
            .Where(run => run.StartedAtUtc >= since)
            .Select(run => new
            {
                run.JobName,
                run.StartedAtUtc,
                run.Outcome,
                run.ItemsProcessed,
                run.Error
            })
            .ToListAsync(cancellationToken);

        var runsByJob = runs
            .GroupBy(run => run.JobName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(run => run.StartedAtUtc).ToList(), StringComparer.Ordinal);

        var jobs = new List<JobHealthDto>();
        foreach (var jobName in PlatformJobNames.Watched)
        {
            runsByJob.TryGetValue(jobName, out var jobRuns);
            var lastRun = jobRuns?.FirstOrDefault();
            var lastSuccessAtUtc = jobRuns?.FirstOrDefault(run => run.Outcome == PlatformJobOutcomeNames.Succeeded)?.StartedAtUtc;
            var consecutiveFailures = jobRuns is null
                ? 0
                : jobRuns.TakeWhile(run => run.Outcome == PlatformJobOutcomeNames.Failed).Count();

            jobs.Add(new JobHealthDto(
                jobName,
                lastRun?.StartedAtUtc,
                lastSuccessAtUtc,
                lastRun?.Outcome,
                lastRun?.ItemsProcessed ?? 0,
                lastRun?.Error,
                consecutiveFailures));
        }

        var stuckBefore = now - healthOptions.Value.QueueStuckThreshold;

        var notificationPending = await dbContext.NotificationOutbox
            .CountAsync(row => row.Status == NotificationOutboxStatus.Pending, cancellationToken);
        var notificationFailed = await dbContext.NotificationOutbox
            .CountAsync(row => row.Status == NotificationOutboxStatus.Failed, cancellationToken);
        var notificationStuck = await dbContext.NotificationOutbox
            .CountAsync(row => row.Status == NotificationOutboxStatus.Pending && row.CreatedUtc < stuckBefore, cancellationToken);

        var billingOutboxPending = await dbContext.OutboxMessages
            .CountAsync(row => row.Status == OutboxMessageStatus.Pending, cancellationToken);
        var billingOutboxFailed = await dbContext.OutboxMessages
            .CountAsync(row => row.Status == OutboxMessageStatus.Failed, cancellationToken);
        var billingOutboxStuck = await dbContext.OutboxMessages
            .CountAsync(row => row.Status == OutboxMessageStatus.Pending && row.CreatedAtUtc < stuckBefore, cancellationToken);

        var queues = new List<QueueHealthDto>
        {
            new(PlatformQueueNames.Notifications, notificationPending, notificationFailed, notificationStuck),
            new(PlatformQueueNames.BillingOutbox, billingOutboxPending, billingOutboxFailed, billingOutboxStuck)
        };

        var openIncidents = await incidentService.ListOpenAsync(cancellationToken);
        var incidents = openIncidents
            .Select(incident => new IncidentDto(
                incident.PlatformIncidentId,
                incident.Kind,
                incident.DedupKey,
                incident.Severity,
                incident.DetailsJson,
                incident.OpenedAtUtc,
                incident.LastSeenAtUtc))
            .ToList();

        return new PlatformHealthOverviewDto(now, jobs, queues, incidents);
    }
}
