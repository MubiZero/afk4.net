using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record ShiftDto(
    Guid ShiftId,
    Guid OrganizationId,
    Guid BranchId,
    Guid OpenedByStaffUserId,
    Guid? ClosedByStaffUserId,
    string State,
    MoneyDto StartingCash,
    MoneyDto? CountedCash,
    MoneyDto? ExpectedCash,
    MoneyDto? Difference,
    string OpeningNote,
    string ClosingNote,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc);
