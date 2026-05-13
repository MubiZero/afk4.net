using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public interface IFloorMapReadService
{
    Task<FloorMapDto?> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken);
}
