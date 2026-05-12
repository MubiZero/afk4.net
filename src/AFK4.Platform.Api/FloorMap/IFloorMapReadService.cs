using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public interface IFloorMapReadService
{
    FloorMapDto GetFloorMap(Guid branchId);
}
