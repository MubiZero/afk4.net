using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.FloorMap;

public sealed class UnconfiguredOperatorFloorMapApiClient : IOperatorFloorMapApiClient
{
    public Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Operator floor map API client is not configured.");
    }
}
