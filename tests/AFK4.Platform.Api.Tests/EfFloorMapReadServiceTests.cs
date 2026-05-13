using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.FloorMap;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfFloorMapReadServiceTests
{
    [Fact]
    public async Task GetFloorMapAsync_ReturnsPersistedSeatsWithAttachedDeviceState()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var lockedSeatId = Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414");
        var maintenanceSeatId = Guid.Parse("ad63d1ef-8477-476b-a21c-06916dd5ad76");
        var assignmentId = Guid.Parse("9a4ad2f7-b74f-4d31-a5c5-7a3d7e2e9921");
        var now = DateTimeOffset.Parse("2026-05-12T00:00:00Z");

        await using (var db = new PlatformDbContext(options))
        {
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = TestIds.OrganizationId,
                Name = "Demo Org",
                CreatedAtUtc = now
            });
            db.Branches.Add(new BranchEntity
            {
                BranchId = TestIds.BranchId,
                OrganizationId = TestIds.OrganizationId,
                Name = "Downtown Branch",
                CreatedAtUtc = now
            });
            db.Zones.Add(new ZoneEntity
            {
                ZoneId = zoneId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "Main Hall",
                SortOrder = 1,
                CreatedAtUtc = now
            });
            db.Seats.AddRange(
                new SeatEntity
                {
                    SeatId = lockedSeatId,
                    OrganizationId = TestIds.OrganizationId,
                    BranchId = TestIds.BranchId,
                    ZoneId = zoneId,
                    Name = "PC-001",
                    SortOrder = 10,
                    CreatedAtUtc = now
                },
                new SeatEntity
                {
                    SeatId = maintenanceSeatId,
                    OrganizationId = TestIds.OrganizationId,
                    BranchId = TestIds.BranchId,
                    ZoneId = zoneId,
                    Name = "PC-002",
                    SortOrder = 20,
                    CreatedAtUtc = now
                });
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = TestIds.DeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-001",
                AgentVersion = "0.1.1",
                ShellVersion = "0.1.2",
                EnrolledAtUtc = now,
                LastHeartbeatAtUtc = now.AddMinutes(2),
                IsOnline = true,
                IsLocked = true
            });
            db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = assignmentId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = lockedSeatId,
                DeviceId = TestIds.DeviceId,
                AttachedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = new PlatformDbContext(options);
        var service = new EfFloorMapReadService(readDb);

        var floorMap = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(floorMap);
        Assert.Equal("Downtown Branch", floorMap.BranchName);
        Assert.Collection(
            floorMap.Seats,
            seat =>
            {
                Assert.Equal(lockedSeatId, seat.SeatId);
                Assert.Equal(zoneId, seat.ZoneId);
                Assert.Equal("Locked", seat.State);
                Assert.Equal(TestIds.DeviceId, seat.DeviceId);
                Assert.Equal("PC-001", seat.DeviceName);
                Assert.True(seat.IsDeviceOnline);
                Assert.True(seat.IsDeviceLocked);
                Assert.Equal("0.1.1", seat.AgentVersion);
                Assert.Equal("0.1.2", seat.ShellVersion);
            },
            seat =>
            {
                Assert.Equal(maintenanceSeatId, seat.SeatId);
                Assert.Equal("Maintenance", seat.State);
                Assert.Null(seat.DeviceId);
            });
    }
}
