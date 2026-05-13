using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record CashMovementDto(
    Guid CashMovementId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ShiftId,
    Guid CreatedByStaffUserId,
    string MovementType,
    MoneyDto Amount,
    string Reason,
    DateTimeOffset CreatedAtUtc);
