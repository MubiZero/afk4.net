using AFK4.Shared.Contracts.Tournaments;

namespace AFK4.Platform.Api.Tournaments;

/// <summary>
/// События клуба: расписание, запись и взнос. Один сервис на обе стороны намеренно — правила
/// «можно ли ещё записаться» и «сколько мест осталось» одни и те же, а две копии разошлись бы
/// в первый же месяц: игрок читал бы «есть места», получая отказ.
/// </summary>
public interface ITournamentService
{
    Task<IReadOnlyList<TournamentDto>> ListForClubAsync(Guid organizationId, Guid branchId, CancellationToken ct);

    Task<TournamentResult<TournamentDto>> CreateAsync(
        Guid organizationId, Guid actorStaffUserId, CreateTournamentRequest request, CancellationToken ct);

    Task<TournamentResult<TournamentDto>> UpdateAsync(
        Guid organizationId, Guid tournamentId, UpdateTournamentRequest request, CancellationToken ct);

    Task<TournamentResult<TournamentDto>> PublishAsync(Guid organizationId, Guid tournamentId, CancellationToken ct);

    Task<TournamentResult<TournamentDto>> CancelAsync(
        Guid organizationId, Guid tournamentId, Guid actorStaffUserId, string reason, CancellationToken ct);

    Task<TournamentResult<IReadOnlyList<TournamentParticipantDto>>> ListParticipantsAsync(
        Guid organizationId, Guid tournamentId, CancellationToken ct);

    Task<IReadOnlyList<PlayerTournamentDto>> ListForPlayerAsync(
        Guid organizationId, Guid branchId, Guid playerAccountId, CancellationToken ct);

    Task<TournamentResult<PlayerTournamentDto>> RegisterAsync(
        Guid organizationId, Guid playerAccountId, Guid tournamentId, CancellationToken ct);

    Task<TournamentResult<PlayerTournamentDto>> CancelRegistrationAsync(
        Guid organizationId, Guid playerAccountId, Guid tournamentId, CancellationToken ct);
}

/// <summary>
/// Исход операции. `NotFound` отдельно от отказа: «такого события нет» и «записаться нельзя» —
/// разные новости и разные ответы HTTP.
/// </summary>
public sealed record TournamentResult<T>(bool Succeeded, bool NotFound, string? Error, T? Value)
{
    public static TournamentResult<T> Ok(T value) => new(true, false, null, value);

    public static TournamentResult<T> Missing() => new(false, true, null, default);

    /// <param name="error">Код из <see cref="TournamentRefusalCodes"/> — фразу собирает клиент.</param>
    public static TournamentResult<T> Refused(string error) => new(false, false, error, default);
}
