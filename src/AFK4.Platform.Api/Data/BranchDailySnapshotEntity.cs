namespace AFK4.Platform.Api.Data;

/// <summary>
/// Свёрнутые сутки одного филиала. Первые три метрики выводимы из событий и задним числом, но
/// хранятся, чтобы вкладка стоила один дешёвый запрос и чтобы «клуб был мёртв 12 дней из 30»
/// вообще существовало как факт, а не как результат ежеразового пересчёта.
/// </summary>
public sealed class BranchDailySnapshotEntity
{
    public Guid BranchDailySnapshotId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>
    /// Календарные сутки в ЧАСОВОМ ПОЯСЕ ФИЛИАЛА (<see cref="BranchEntity.PreferredTimeZone"/>),
    /// а не в UTC: клуб в UTC+5 работает ночью, и по UTC-суткам вся выручка с полуночи до пяти
    /// утра падала бы во «вчера».
    /// </summary>
    public DateOnly SnapshotDate { get; set; }

    public int SessionCount { get; set; }

    public long RevenueMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public int ShiftOpenedCount { get; set; }

    /// <summary>
    /// Выходил ли клуб на связь. Единственное поле, не выводимое задним числом: heartbeat
    /// перезаписывается. <c>null</c> — «неизвестно»: так помечается день, доснятый после простоя
    /// самой платформы. Записать сюда <c>false</c> значило бы обвинить клуб в нашем простое.
    /// </summary>
    public bool? AgentAlive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
