using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopOrderDto(
    Guid Id,
    Guid BranchId,
    Guid SeatId,
    Guid PlayerAccountId,
    string PlayerDisplayName,
    string Status,
    MoneyDto Total,
    IReadOnlyList<ShopOrderLineDto> Lines,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? CancelledAtUtc,
    int Version,
    Guid? PosSaleId = null);
