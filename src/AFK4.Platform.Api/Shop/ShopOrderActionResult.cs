using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public sealed record ShopOrderActionResult(
    bool Succeeded,
    bool NotFound,
    bool Conflict,
    string? ErrorCode,
    ShopOrderDto? Order,
    int? CurrentVersion)
{
    public static ShopOrderActionResult Ok(ShopOrderDto order) =>
        new(true, false, false, null, order, null);

    public static ShopOrderActionResult Business(string errorCode) =>
        new(false, false, false, errorCode, null, null);

    public static ShopOrderActionResult Missing() =>
        new(false, true, false, null, null, null);

    public static ShopOrderActionResult VersionConflict(int? currentVersion) =>
        new(false, false, true, "version_conflict", null, currentVersion);
}
