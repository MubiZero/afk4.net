using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record RecordCashMovementRequest(
    Guid OrganizationId,
    string MovementType,
    MoneyDto Amount,
    string Reason,
    string IdempotencyKey);
