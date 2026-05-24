using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public interface IFloorMapReadService
{
    Task<FloorMapReadResult?> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken);
}

public sealed record FloorMapReadResult(FloorMapDto FloorMap, string ETag);
