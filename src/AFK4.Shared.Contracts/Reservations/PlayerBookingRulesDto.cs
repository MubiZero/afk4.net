namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Правила брони этого филиала для этого игрока — то, чем приложение объясняет «так решил клуб».
///
/// Всё посчитано сервером под конкретного человека: предоплата нужна именно ему, потолок броней
/// именно его. Ни одного поля про других игроков здесь нет и быть не должно — иначе приложение
/// одного клуба становится окном в клиентскую базу.
/// </summary>
/// <param name="MaxActiveReservations">
/// Пусто — значит потолка нет: игрок в этом филиале уже свой.
/// </param>
public sealed record PlayerBookingRulesDto(
    Guid BranchId,
    string AcceptanceMode,
    int RespondWithinMinutes,
    bool PrepaymentRequired,
    int ActiveReservations,
    int? MaxActiveReservations,
    int HoldSeatAfterStartMinutes);
