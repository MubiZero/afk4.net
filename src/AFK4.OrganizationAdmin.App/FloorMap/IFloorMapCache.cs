using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.OrganizationAdmin.App.FloorMap;

/// <summary>
/// Last-known-good floor map snapshot persisted locally per branch (spec §6.5). Lets the operator
/// surface keep rendering seats — read-only, visibly stale — when the platform is unreachable.
/// </summary>
public interface IFloorMapCache
{
    void Save(FloorMapDto floorMap, DateTimeOffset cachedAtUtc);

    FloorMapCacheEntry? Load(Guid branchId);
}

public sealed record FloorMapCacheEntry(FloorMapDto FloorMap, DateTimeOffset CachedAtUtc);
