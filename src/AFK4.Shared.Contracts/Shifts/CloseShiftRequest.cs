using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record CloseShiftRequest(
    Guid OrganizationId,
    MoneyDto CountedCash,
    string ClosingNote,
    string IdempotencyKey);
