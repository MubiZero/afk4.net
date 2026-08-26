namespace AFK4.Platform.Api.Data;

/// <summary>
/// Запись игрока на событие клуба. Отменённая запись остаётся строкой: деньги по ней ходили,
/// и «пропала запись» читалось бы как пропажа денег.
/// </summary>
public sealed class TournamentRegistrationEntity
{
    public Guid TournamentRegistrationId { get; set; }

    public Guid TournamentId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid PlayerAccountId { get; set; }

    /// <see cref="AFK4.Shared.Contracts.Tournaments.TournamentRegistrationStateNames"/>.
    public string State { get; set; } = string.Empty;

    /// Сколько человек заплатил за участие. Хранится на записи, а не берётся из события:
    /// клуб может поменять взнос завтра, а заплачено было по вчерашнему.
    public long EntryFeeMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTimeOffset RegisteredAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }
}
