using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Pos;

public sealed record PosSaleLineDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    MoneyDto UnitPrice,
    MoneyDto LineTotal);
