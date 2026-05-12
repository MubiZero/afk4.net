using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public sealed class InMemoryFloorMapReadService : IFloorMapReadService
{
    public FloorMapDto GetFloorMap(Guid branchId)
    {
        return new FloorMapDto(
            BranchId: branchId,
            BranchName: "Demo Branch",
            Seats:
            [
                new SeatStatusDto(
                    SeatId: Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414"),
                    SeatName: "PC-001",
                    ZoneName: "Main Hall",
                    State: "Free",
                    ActiveSessionId: null,
                    RemainingSeconds: null),
                new SeatStatusDto(
                    SeatId: Guid.Parse("ad63d1ef-8477-476b-a21c-06916dd5ad76"),
                    SeatName: "PC-002",
                    ZoneName: "Main Hall",
                    State: "Locked",
                    ActiveSessionId: null,
                    RemainingSeconds: null)
            ]);
    }
}
