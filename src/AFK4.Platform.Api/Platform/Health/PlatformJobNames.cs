namespace AFK4.Platform.Api.Platform.Health;

/// <summary>Имена периодических заданий — общий словарь для регистратора прогонов, правил сторожа и экрана.</summary>
public static class PlatformJobNames
{
    public const string InvoiceGeneration = "invoice_generation";
    public const string BillingOutbox = "billing_outbox";
    public const string NotificationDispatch = "notification_dispatch";
    public const string DailySummary = "daily_summary";
    public const string ScheduledReports = "scheduled_reports";
    public const string AutoProtection = "auto_protection";
    public const string HealthWatch = "health_watch";
    public const string SubscriptionSnapshots = "subscription_snapshots";
    public const string BranchSnapshots = "branch_snapshots";

    /// <summary>Возврат замороженных денег и освобождение места, когда игрок не пришёл.</summary>
    public const string ReservationNoShow = "reservation_no_show";

    /// <summary>Снятие заявок, на которые клуб не ответил в обещанный срок, с полным возвратом денег.</summary>
    public const string ReservationRequestExpiry = "reservation_request_expiry";

    /// <summary>Напоминания игроку, у которых нет события: конец сессии и приближающаяся бронь.</summary>
    public const string PlayerReminders = "player_reminders";

    /// <summary>Суточный пересчёт сетевой репутации: клуб видит вчерашнюю правду, а не живой счётчик.</summary>
    public const string ReputationSnapshot = "reputation_snapshot";

    /// <summary>Доставка оповещений мимо очереди — результат тоже записывается как прогон.</summary>
    public const string AlertDelivery = "alert_delivery";

    /// <summary>
    /// Задания, за которыми следят правила здоровья. AlertDelivery сюда НЕ входит: он не
    /// периодический, его прогон появляется только когда есть о чём оповещать, и ждать его
    /// по расписанию значило бы заводить инцидент за тишину, которая означает «всё хорошо».
    /// </summary>
    public static readonly IReadOnlyList<string> Watched =
    [
        InvoiceGeneration,
        BillingOutbox,
        NotificationDispatch,
        DailySummary,
        ScheduledReports,
        AutoProtection,
        HealthWatch,
        SubscriptionSnapshots,
        BranchSnapshots,
        ReservationNoShow,
        ReservationRequestExpiry,
        PlayerReminders,
        ReputationSnapshot
    ];
}
