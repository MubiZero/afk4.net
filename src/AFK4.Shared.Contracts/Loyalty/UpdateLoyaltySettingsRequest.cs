namespace AFK4.Shared.Contracts.Loyalty;

public sealed record UpdateLoyaltySettingsRequest(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints);
