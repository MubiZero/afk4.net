using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Loyalty;

/// <summary>
/// Кешбэк игрока: сколько накоплено и по каким правилам начисляется.
///
/// Кешбэк — не баллы: он приходит на кошелёк обычными деньгами, и тратится так же.
/// </summary>
public sealed record PlayerLoyaltyDto(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints,
    bool SessionEnabled,
    int SessionPercentBasisPoints,
    MoneyDto TotalEarned,
    IReadOnlyList<CashbackEntryDto> Recent);
