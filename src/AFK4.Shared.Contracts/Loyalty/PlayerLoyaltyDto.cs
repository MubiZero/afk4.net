using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Loyalty;

public sealed record PlayerLoyaltyDto(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints,
    bool SessionEnabled,
    int SessionPercentBasisPoints,
    MoneyDto TotalEarned,
    IReadOnlyList<CashbackEntryDto> Recent);
