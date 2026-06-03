using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record CloseShiftRequest(
    Guid OrganizationId,
    MoneyDto CountedCash,
    string ClosingNote,
    string IdempotencyKey,
    // Anti-fraud §5.7: required only when the cash discrepancy exceeds the branch tolerance; must be a
    // manager other than the operator who opened or closed the shift.
    Guid? ManagerSignOffStaffUserId = null,
    string? SignOffReason = null);
