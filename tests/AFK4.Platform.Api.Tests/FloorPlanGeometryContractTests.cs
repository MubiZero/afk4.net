using System.Text.Json;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.Tests;

public sealed class FloorPlanGeometryContractTests
{
    [Fact]
    public void SeatStatusDto_RoundTripsGeometryFields()
    {
        var seat = new SeatStatusDto(
            SeatId: Guid.NewGuid(),
            SeatName: "PC-001",
            ZoneId: Guid.NewGuid(),
            ZoneName: "Main Hall",
            SortOrder: 10,
            State: "Free",
            DeviceId: null,
            DeviceName: null,
            IsDeviceOnline: null,
            IsDeviceLocked: null,
            LastHeartbeatAtUtc: null,
            AgentVersion: null,
            ShellVersion: null,
            ActiveSessionId: null,
            RemainingSeconds: null,
            PosX: 3,
            PosY: 5,
            Rotation: 90,
            SeatType: "console");

        var roundTripped = JsonSerializer.Deserialize<SeatStatusDto>(JsonSerializer.Serialize(seat))!;

        Assert.Equal(3, roundTripped.PosX);
        Assert.Equal(5, roundTripped.PosY);
        Assert.Equal(90, roundTripped.Rotation);
        Assert.Equal("console", roundTripped.SeatType);
    }

    [Fact]
    public void FloorMapDto_RoundTripsZoneGeometryAndWalls()
    {
        var dto = new FloorMapDto(Guid.NewGuid(), "Branch", [])
        {
            Zones =
            [
                new FloorMapZoneDto(Guid.NewGuid(), "VIP", 1)
                {
                    GeoX = 1, GeoY = 2, GeoWidth = 4, GeoHeight = 3, Color = "#22c55e", ZoneType = "lounge"
                }
            ],
            Walls = [new FloorMapWallDto(Guid.NewGuid(), 0, 0, 10, 0)]
        };

        var roundTripped = JsonSerializer.Deserialize<FloorMapDto>(JsonSerializer.Serialize(dto))!;

        var zone = Assert.Single(roundTripped.Zones);
        Assert.Equal(4, zone.GeoWidth);
        Assert.Equal("lounge", zone.ZoneType);
        var wall = Assert.Single(roundTripped.Walls);
        Assert.Equal(10, wall.X2);
    }

    [Fact]
    public void BulkUpdateRequest_DefaultsWallsToEmptyAndGeometryToNull()
    {
        var request = new FloorMapBulkUpdateRequest(
            Guid.NewGuid(),
            [new FloorMapBulkZoneRequest(null, "z1", "Hall", 1)],
            [new FloorMapBulkSeatRequest(null, "s1", "z1", "PC-1", 1)]);

        Assert.Null(request.Walls);
        Assert.Null(request.Seats[0].PosX);
        Assert.Equal("pc", request.Seats[0].SeatType);
        Assert.Equal(0, request.Seats[0].Rotation);
    }
}
