namespace AFK4.Shared.Contracts.Platform.Health;

/// <summary>
/// Состояние одного задания. Kind/JobName едут кодом: клиент никогда не рендерит серверную
/// строку как пользовательский текст — у каждого имени есть перевод в каталоге.
/// </summary>
public sealed record JobHealthDto(
    string JobName,
    DateTimeOffset? LastRunAtUtc,
    DateTimeOffset? LastSuccessAtUtc,
    string? LastOutcome,
    int LastItemsProcessed,
    string? LastError,
    int ConsecutiveFailures);

public sealed record QueueHealthDto(string QueueName, int PendingCount, int FailedCount, int StuckCount);

public sealed record IncidentDto(
    Guid IncidentId,
    string Kind,
    string DedupKey,
    string Severity,
    string DetailsJson,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset LastSeenAtUtc);

public sealed record PlatformHealthOverviewDto(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<JobHealthDto> Jobs,
    IReadOnlyList<QueueHealthDto> Queues,
    IReadOnlyList<IncidentDto> OpenIncidents);

public static class PlatformQueueNames
{
    public const string Notifications = "notifications";
    public const string BillingOutbox = "billing_outbox";
}
