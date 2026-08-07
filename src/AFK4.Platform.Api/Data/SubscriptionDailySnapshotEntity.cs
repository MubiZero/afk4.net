namespace AFK4.Platform.Api.Data;

/// <summary>
/// Состояние подписки организации на конец суток. Заводится потому, что состояние — не событие:
/// подписка хранит только сегодняшний статус, и клуб, ушедший в июне, сегодня в базе неотличим
/// от того, кто не платил никогда. Приход и отток считаются только отсюда.
/// </summary>
public sealed class SubscriptionDailySnapshotEntity
{
    public Guid SubscriptionDailySnapshotId { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Сутки в UTC, к которым относится снимок (без времени).</summary>
    public DateOnly SnapshotDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string PlanCode { get; set; } = string.Empty;

    /// <summary>Цена, приведённая к месяцу и с учётом действующей скидки — то, что клуб реально платит.</summary>
    public long MonthlyAmountMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
