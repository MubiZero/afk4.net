using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.OrganizationAdmin.App.FloorMap;

public interface IOperatorFloorMapApiClient
{
    Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken);
}
