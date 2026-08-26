namespace AFK4.Platform.Api.Data;

/// <summary>
/// Событие клуба: турнир, ночь игры, чемпионат зала. Живёт в конкретном зале и в конкретный
/// вечер — «событие сети» ввело бы игрока в заблуждение, потому что приехать он может только
/// в один из залов.
/// </summary>
public sealed class TournamentEntity
{
    public Guid TournamentId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// Игра или дисциплина словами клуба («Dota 2», «FIFA», «Ночь Counter-Strike»). Пусто —
    /// клуб не уточнил, и выдумывать за него нечего.
    public string Discipline { get; set; } = string.Empty;

    public DateTimeOffset StartsAtUtc { get; set; }

    /// Взнос за участие в валюте клуба. 0 — участие бесплатное, и это обычный случай для
    /// вечера, которым клуб просто заполняет будний день.
    public long EntryFeeMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    /// Сколько человек берут. 0 — без ограничения: у клуба на сорок мест это честнее, чем
    /// выдуманное число.
    public int Capacity { get; set; }

    /// <see cref="AFK4.Shared.Contracts.Tournaments.TournamentStateNames"/>.
    public string State { get; set; } = string.Empty;

    public Guid CreatedByStaffUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    /// Почему клуб отменил событие. Игрок читает это вместо «отменено» — как в отказе по броне.
    public string CancelReason { get; set; } = string.Empty;
}
