using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Inventory;

public sealed record CreateStockMovementRequest(
    Guid OrganizationId,
    Guid ProductId,
    string MovementType,
    int QuantityDelta,
    MoneyDto UnitCost,
    string Reason,
    string IdempotencyKey);
