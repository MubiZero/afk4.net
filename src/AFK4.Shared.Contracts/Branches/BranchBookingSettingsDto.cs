namespace AFK4.Shared.Contracts.Branches;

/// <summary>
/// Настройки приёма гостей у филиала — то, что видит и правит клуб.
///
/// <paramref name="UpdatedAtUtc"/> пуст, пока филиал ничего не настраивал: значения в этом случае
/// не «нулевые», а по умолчанию, и админу полезно отличать одно от другого.
/// </summary>
public sealed record BranchBookingSettingsDto(
    Guid OrganizationId,
    Guid BranchId,
    string AcceptanceMode,
    int RespondWithinMinutes,
    bool RequirePrepaymentFromNewGuests,
    int MaxActiveReservationsForNewGuests,
    int RegularAfterVisits,
    int HoldSeatAfterStartMinutes,
    bool KeepPrepaymentOnNoShow,
    DateTimeOffset? UpdatedAtUtc);
