namespace AFK4.Shared.Contracts.Branches;

public sealed record UpdateBranchBookingSettingsRequest(
    Guid OrganizationId,
    string AcceptanceMode,
    int RespondWithinMinutes,
    bool RequirePrepaymentFromNewGuests,
    int MaxActiveReservationsForNewGuests,
    int RegularAfterVisits,
    int HoldSeatAfterStartMinutes,
    bool KeepPrepaymentOnNoShow);
