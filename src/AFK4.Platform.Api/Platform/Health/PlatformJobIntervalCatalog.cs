using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Единственный источник интервалов наблюдаемых заданий (<see cref="PlatformJobNames.Watched"/>).
/// И сторож (<see cref="PlatformHealthWatchJob"/>), и обзор здоровья строят снимок по одному
/// и тому же словарю — раньше он жил только внутри сторожа, и обзор рисковал завести свою,
/// неизбежно расходящуюся копию.
/// </summary>
public sealed class PlatformJobIntervalCatalog(
    IOptions<PlatformHealthOptions> healthOptions,
    IOptions<BillingOptions> billingOptions,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<OutboxOptions> outboxOptions,
    AutoProtectionOptions autoProtectionOptions,
    IOptions<PlatformAnalyticsOptions> analyticsOptions)
{
    public IReadOnlyDictionary<string, TimeSpan> Build() => new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
    {
        [PlatformJobNames.InvoiceGeneration] = billingOptions.Value.GenerationInterval,
        [PlatformJobNames.BillingOutbox] = outboxOptions.Value.PollInterval,
        [PlatformJobNames.NotificationDispatch] = notificationOptions.Value.PollInterval,
        [PlatformJobNames.DailySummary] = notificationOptions.Value.DailySummaryInterval,
        [PlatformJobNames.ScheduledReports] = notificationOptions.Value.ScheduledReportInterval,
        [PlatformJobNames.AutoProtection] = autoProtectionOptions.TickInterval,
        [PlatformJobNames.HealthWatch] = healthOptions.Value.WatchInterval,
        [PlatformJobNames.SubscriptionSnapshots] = analyticsOptions.Value.SnapshotInterval
    };
}
