using System.Globalization;
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

public sealed record JobState(string JobName, TimeSpan Interval, DateTimeOffset? LastSuccessAtUtc, int ConsecutiveFailures);

public sealed record HealthSnapshot(
    IReadOnlyList<JobState> Jobs,
    int NotificationFailed,
    int NotificationStuck,
    int BillingOutboxFailed,
    int BillingOutboxStuck,
    DateTimeOffset ProcessStartedAtUtc);

public sealed record DetectedProblem(string Kind, string DedupKey, string Severity, string DetailsJson);

/// <summary>
/// Правила здоровья — чистая функция от снимка состояния. Пороги берутся из интервала самого
/// задания, а не из отдельной таблицы порогов: интервал уже живёт в опциях задания, и второй
/// источник правды разошёлся бы с первым при первом же изменении.
/// </summary>
public static class PlatformHealthRules
{
    /// <summary>Нижняя граница окна ожидания: у задания с интервалом в 10 секунд тройной интервал — это шум.</summary>
    private static readonly TimeSpan MinimumOverdueWindow = TimeSpan.FromMinutes(15);

    private const int FailureStreakThreshold = 3;

    /// <summary>
    /// Окно, после которого задание считается просроченным. Публичный, чтобы сторож мог выбрать
    /// глубину истории прогонов от него же — единый источник правды на оба потребителя, иначе
    /// заданию с интервалом больше, чем зашитая глубина выборки, окно просрочки молча перестанет
    /// работать (последний успешный прогон просто не попадёт в выборку).
    /// </summary>
    public static TimeSpan OverdueWindow(TimeSpan interval) => Max(interval * 3, MinimumOverdueWindow);

    /// <summary>Виды инцидентов, которые умеет обнаруживать этот набор правил — ровно их и закрывает сторож.</summary>
    public static readonly IReadOnlyList<string> EvaluatedKinds =
    [
        PlatformIncidentKindNames.JobOverdue,
        PlatformIncidentKindNames.JobFailing,
        PlatformIncidentKindNames.NotificationQueueStuck,
        PlatformIncidentKindNames.BillingOutboxStuck
    ];

    /// <summary>Задания, чья остановка стоит денег: счета не выставляются, письма не уходят.</summary>
    private static readonly HashSet<string> CriticalJobs = new(StringComparer.Ordinal)
    {
        PlatformJobNames.InvoiceGeneration,
        PlatformJobNames.BillingOutbox,
        PlatformJobNames.NotificationDispatch
    };

    public static IReadOnlyList<DetectedProblem> Evaluate(HealthSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var problems = new List<DetectedProblem>();

        foreach (var job in snapshot.Jobs)
        {
            var window = OverdueWindow(job.Interval);
            var overdueSince = job.LastSuccessAtUtc;

            // Задание без единого успешного прогона: все семь заданий стартуют одновременно
            // как независимые фоновые задачи при старте процесса, и на пустой таблице прогонов
            // "никогда не запускалось" означает "ещё прогревается", а не "сломано". Считаем окно
            // просрочки от момента старта процесса, а не от несуществующего последнего успеха —
            // иначе первый же снимок здоровья (сторож тоже стартует сразу) кричит критикой про
            // счета, которые просто ещё не успели выставиться.
            var isOverdue = overdueSince is null
                ? now - snapshot.ProcessStartedAtUtc > window
                : now - overdueSince.Value > window;

            if (isOverdue)
            {
                var minutes = overdueSince is null ? -1 : (int)(now - overdueSince.Value).TotalMinutes;
                problems.Add(new DetectedProblem(
                    PlatformIncidentKindNames.JobOverdue,
                    $"{PlatformIncidentKindNames.JobOverdue}:{job.JobName}",
                    CriticalJobs.Contains(job.JobName)
                        ? PlatformIncidentSeverityNames.Critical
                        : PlatformIncidentSeverityNames.Warning,
                    Details(("job", job.JobName), ("minutes", minutes.ToString(CultureInfo.InvariantCulture)))));
            }

            if (job.ConsecutiveFailures >= FailureStreakThreshold)
            {
                problems.Add(new DetectedProblem(
                    PlatformIncidentKindNames.JobFailing,
                    $"{PlatformIncidentKindNames.JobFailing}:{job.JobName}",
                    PlatformIncidentSeverityNames.Warning,
                    Details(("job", job.JobName), ("failures", job.ConsecutiveFailures.ToString(CultureInfo.InvariantCulture)))));
            }
        }

        if (snapshot.NotificationFailed > 0 || snapshot.NotificationStuck > 0)
        {
            problems.Add(new DetectedProblem(
                PlatformIncidentKindNames.NotificationQueueStuck,
                PlatformIncidentKindNames.NotificationQueueStuck,
                PlatformIncidentSeverityNames.Critical,
                Details(
                    ("failed", snapshot.NotificationFailed.ToString(CultureInfo.InvariantCulture)),
                    ("stuck", snapshot.NotificationStuck.ToString(CultureInfo.InvariantCulture)))));
        }

        if (snapshot.BillingOutboxFailed > 0 || snapshot.BillingOutboxStuck > 0)
        {
            problems.Add(new DetectedProblem(
                PlatformIncidentKindNames.BillingOutboxStuck,
                PlatformIncidentKindNames.BillingOutboxStuck,
                PlatformIncidentSeverityNames.Critical,
                Details(
                    ("failed", snapshot.BillingOutboxFailed.ToString(CultureInfo.InvariantCulture)),
                    ("stuck", snapshot.BillingOutboxStuck.ToString(CultureInfo.InvariantCulture)))));
        }

        return problems;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;

    // Детали — только числа и идентификаторы. Готовую фразу здесь собирать нельзя:
    // текст живёт в каталоге переводов, иначе панель на таджикском покажет русскую строку.
    private static string Details(params (string Key, string Value)[] pairs) =>
        '{' + string.Join(',', pairs.Select(pair => $"\"{pair.Key}\":\"{pair.Value}\"")) + '}';
}
