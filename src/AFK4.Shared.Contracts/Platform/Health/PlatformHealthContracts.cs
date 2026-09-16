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

/// <summary>
/// Одна провалившаяся строка очереди. Без причины провала счётчик «провалено» не диагностируем:
/// видно, что письма не уходят, и не видно почему. Адрес маскирован — домен для разбора важен,
/// полный адрес человека нет.
/// </summary>
public sealed record QueueFailureDto(
    string QueueName,
    DateTimeOffset? FailedAtUtc,
    string Kind,
    string RecipientMasked,
    int AttemptCount,
    string? LastError);

public sealed record PlatformHealthOverviewDto(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<JobHealthDto> Jobs,
    IReadOnlyList<QueueHealthDto> Queues,
    IReadOnlyList<IncidentDto> OpenIncidents,
    IReadOnlyList<QueueFailureDto> RecentFailures,
    /// Хранилище файлов не настроено: логотипы и фото зала загрузить нельзя ни из мастера, ни из
    /// панели. Видно здесь, а не при первой попытке загрузки — иначе об этом узнаёт клуб, а не мы.
    bool MediaStorageConfigured = true,
    /// Резервный канал критических оповещений: SMS уходят только по одобренному шаблону шлюза, и
    /// без него канал молчит. Пока это было видно лишь в деталях провалившегося прогона, «почта
    /// умерла — придёт SMS» оставалось обещанием, которое некому было проверить.
    bool AlertSmsConfigured = true);

/// <summary>
/// Проверка доставки почты: письмо уходит боевым путём, а причина отказа возвращается сразу.
/// Раньше единственным способом проверить почту было послать кому-то настоящее приглашение и
/// гадать по счётчику «провалено» без текста ошибки.
/// </summary>
public sealed record SendTestEmailRequest(string Email);

public sealed record SendTestEmailResultDto(bool Delivered, string? Error);

public static class PlatformQueueNames
{
    public const string Notifications = "notifications";
    public const string BillingOutbox = "billing_outbox";
}
