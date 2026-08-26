using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Tournaments;

/// <summary>Событие клуба глазами стойки: всё, включая черновики и счётчик записавшихся.</summary>
public sealed record TournamentDto(
    Guid TournamentId,
    Guid BranchId,
    string Title,
    string Description,
    string Discipline,
    DateTimeOffset StartsAtUtc,
    MoneyDto EntryFee,
    int Capacity,
    string State,
    int RegisteredCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string CancelReason);

/// <summary>
/// Событие глазами игрока: только то, по чему решают, идти ли. Черновиков здесь не бывает,
/// а вместо списка участников — сколько мест осталось и записан ли он сам.
/// </summary>
public sealed record PlayerTournamentDto(
    Guid TournamentId,
    Guid BranchId,
    string BranchName,
    string Title,
    string Description,
    string Discipline,
    DateTimeOffset StartsAtUtc,
    MoneyDto EntryFee,
    int Capacity,
    int RegisteredCount,
    bool IsRegistered,
    string State,
    string CancelReason);

/// <summary>Кто записался — список для стойки: по нему встречают на входе.</summary>
public sealed record TournamentParticipantDto(
    Guid TournamentRegistrationId,
    Guid PlayerAccountId,
    string DisplayName,
    string? PhoneNumber,
    MoneyDto EntryFeePaid,
    DateTimeOffset RegisteredAtUtc);

public sealed record CreateTournamentRequest(
    Guid BranchId,
    string Title,
    string Description,
    string Discipline,
    DateTimeOffset StartsAtUtc,
    long EntryFeeMinorUnits,
    int Capacity);

/// <summary>
/// Правка события. Незаполненное поле означает «оставить как было» — стойка правит одну строку,
/// а не переписывает событие целиком.
/// </summary>
public sealed record UpdateTournamentRequest(
    string? Title = null,
    string? Description = null,
    string? Discipline = null,
    DateTimeOffset? StartsAtUtc = null,
    long? EntryFeeMinorUnits = null,
    int? Capacity = null);

public sealed record CancelTournamentRequest(string Reason);
