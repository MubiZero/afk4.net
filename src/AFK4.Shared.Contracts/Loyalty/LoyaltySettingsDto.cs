namespace AFK4.Shared.Contracts.Loyalty;

public sealed record LoyaltySettingsDto(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints);
