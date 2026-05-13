using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Inventory;

public sealed record StockMovementDto(
    Guid StockMovementId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ProductId,
    string MovementType,
    int QuantityDelta,
    MoneyDto UnitCost,
    string Reason,
    Guid CreatedByStaffUserId,
    DateTimeOffset CreatedAtUtc);
