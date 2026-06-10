using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Loyalty;

public sealed record PlayerLoyaltyDto(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints,
    MoneyDto TotalEarned,
    IReadOnlyList<CashbackEntryDto> Recent);
