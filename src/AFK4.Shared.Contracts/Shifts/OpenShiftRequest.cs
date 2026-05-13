using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record OpenShiftRequest(
    Guid OrganizationId,
    MoneyDto StartingCash,
    string OpeningNote,
    string IdempotencyKey);
