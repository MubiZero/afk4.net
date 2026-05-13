using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Pos;

public sealed record PosProductDto(
    Guid ProductId,
    Guid OrganizationId,
    Guid BranchId,
    Guid CategoryId,
    string Name,
    string Sku,
    MoneyDto Price,
    bool TrackStock,
    bool AllowNegativeStock,
    bool IsActive,
    int StockOnHand,
    DateTimeOffset CreatedAtUtc);
