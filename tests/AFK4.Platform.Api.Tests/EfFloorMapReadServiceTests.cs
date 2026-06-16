using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
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
        var service = new EfFloorMapReadService(readDb, new FixedTimeProvider(now));

        var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.ETag));
        var floorMap = result.FloorMap;
        Assert.Equal("Downtown Branch", floorMap.BranchName);
        var zone = Assert.Single(floorMap.Zones);
        Assert.Equal(zoneId, zone.ZoneId);
        Assert.Equal("Main Hall", zone.Name);
        Assert.Equal(1, zone.SortOrder);
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

    [Fact]
    public async Task GetFloorMapAsync_OpenTabSession_ReportsLiveAccruedCost()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var seatId = Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414");
        var tariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        var sessionId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
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
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "PC-001",
                SortOrder = 10,
                CreatedAtUtc = now
            });
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = TestIds.DeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-001",
                EnrolledAtUtc = now,
                LastHeartbeatAtUtc = now,
                IsOnline = true,
                IsLocked = false
            });
            db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = seatId,
                DeviceId = TestIds.DeviceId,
                AttachedAtUtc = now
            });
            db.Tariffs.Add(new TariffEntity
            {
                TariffId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "Standard",
                IsActive = true,
                CreatedAtUtc = now
            });
            db.TariffVersions.Add(new TariffVersionEntity
            {
                TariffVersionId = tariffVersionId,
                TariffId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                VersionNumber = 1,
                CurrencyCode = "TJS",
                PricePerMinuteMinorUnits = 50,
                MinimumBillableMinutes = 30,
                RoundingIncrementMinutes = 15,
                EffectiveFromUtc = now.AddDays(-1),
                CreatedAtUtc = now
            });
            db.Sessions.Add(new SessionEntity
            {
                SessionId = sessionId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = seatId,
                DeviceId = TestIds.DeviceId,
                CreatedByStaffUserId = Guid.NewGuid(),
                PlayerKind = "guest",
                PlayerAccountId = Guid.NewGuid(),
                TariffRuleVersionId = tariffVersionId.ToString("D"),
                State = SessionStateNames.Active,
                RequestedAtUtc = now.AddMinutes(-40),
                StartedAtUtc = now.AddMinutes(-40),
                EndsAtUtc = null,
                UpdatedAtUtc = now.AddMinutes(-40)
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = new PlatformDbContext(options);
        var service = new EfFloorMapReadService(readDb, new FixedTimeProvider(now));

        var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(result);
        var seat = Assert.Single(result.FloorMap.Seats);
        Assert.Equal(sessionId, seat.ActiveSessionId);
        Assert.Null(seat.RemainingSeconds);
        // 40 min elapsed -> max(40, min 30) = 40 -> round up to 15-min increment = 45 -> 45 * 50 = 2250.
        Assert.Equal(2250, seat.AccruedCostMinorUnits);
        Assert.Equal("TJS", seat.CurrencyCode);
    }

    [Fact]
    public async Task GetFloorMapAsync_ReturnsEmptyZonesWithoutSeats()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
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
                Name = "Empty VIP",
                SortOrder = 2,
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = new PlatformDbContext(options);
        var service = new EfFloorMapReadService(readDb);

        var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.FloorMap.Seats);
        var zone = Assert.Single(result.FloorMap.Zones);
        Assert.Equal(zoneId, zone.ZoneId);
        Assert.Equal("Empty VIP", zone.Name);
        Assert.Equal(2, zone.SortOrder);
    }

    [Fact]
    public async Task GetFloorMapAsync_DoesNotProjectManagerWorkstationAssignmentsAsPlayableSeats()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var seatId = Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414");
        var now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

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
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "Manager-created seat",
                SortOrder = 10,
                CreatedAtUtc = now
            });
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = TestIds.DeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "MANAGER-01",
                AgentVersion = "0.1.33",
                ShellVersion = string.Empty,
                Role = DeviceRoleNames.ManagerWorkstation,
                EnrollmentState = DeviceEnrollmentStateNames.Approved,
                EnrolledAtUtc = now,
                LastHeartbeatAtUtc = now,
                IsOnline = true,
                IsLocked = false
            });
            db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.Parse("9a4ad2f7-b74f-4d31-a5c5-7a3d7e2e9921"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = seatId,
                DeviceId = TestIds.DeviceId,
                AttachedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = new PlatformDbContext(options);
        var service = new EfFloorMapReadService(readDb);

        var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(result);
        var seat = Assert.Single(result.FloorMap.Seats);
        Assert.Equal("Maintenance", seat.State);
        Assert.Null(seat.DeviceId);
        Assert.Null(seat.IsDeviceOnline);
    }

    [Fact]
    public async Task GetFloorMapAsync_MarksAssignedDeviceOfflineWhenHeartbeatIsStale()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var seatId = Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414");
        var now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

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
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "PC-001",
                SortOrder = 10,
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
                Role = DeviceRoleNames.GamingPc,
                EnrollmentState = DeviceEnrollmentStateNames.Approved,
                EnrolledAtUtc = now.AddHours(-1),
                LastHeartbeatAtUtc = now.AddMinutes(-10),
                IsOnline = true,
                IsLocked = false
            });
            db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.Parse("9a4ad2f7-b74f-4d31-a5c5-7a3d7e2e9921"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = seatId,
                DeviceId = TestIds.DeviceId,
                AttachedAtUtc = now.AddMinutes(-30)
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = new PlatformDbContext(options);
        var service = new EfFloorMapReadService(
            readDb,
            new FixedTimeProvider(now),
            new BranchDiagnosticsOptions { StaleHeartbeatSeconds = 300 });

        var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(result);
        var seat = Assert.Single(result.FloorMap.Seats);
        Assert.Equal("Offline", seat.State);
        Assert.False(seat.IsDeviceOnline);
        Assert.Equal(now.AddMinutes(-10), seat.LastHeartbeatAtUtc);
    }

    [Fact]
    public async Task GetFloorMapAsync_ProjectsActiveSessionOverDeviceState()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var seatId = Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414");
        var sessionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
        var now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

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
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "PC-001",
                SortOrder = 10,
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
                DeviceSeatAssignmentId = Guid.Parse("9a4ad2f7-b74f-4d31-a5c5-7a3d7e2e9921"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = seatId,
                DeviceId = TestIds.DeviceId,
                AttachedAtUtc = now
            });
            db.Sessions.Add(new SessionEntity
            {
                SessionId = sessionId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = seatId,
                DeviceId = TestIds.DeviceId,
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                TariffRuleVersionId = "manual-v1",
                State = SessionStateNames.Active,
                RequestedAtUtc = now,
                StartedAtUtc = now,
                EndsAtUtc = now.AddMinutes(30),
                UpdatedAtUtc = now
            });

            await db.SaveChangesAsync();
        }

        await using var readDb = new PlatformDbContext(options);
        var service = new EfFloorMapReadService(readDb, new FixedTimeProvider(now));

        var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(result);
        var seat = Assert.Single(result.FloorMap.Seats);
        Assert.Equal(sessionId, seat.ActiveSessionId);
        Assert.Equal(1800, seat.RemainingSeconds);
        Assert.Equal("Active", seat.State);
    }

    [Fact]
    public async Task GetFloorMapAsync_SurfacesPlayerNameTariffAndStartForBilledSession_NullForGuest()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var billedSeatId = Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414");
        var guestSeatId = Guid.Parse("ad63d1ef-8477-476b-a21c-06916dd5ad76");
        var tariffId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var tariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        var playerAccountId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var startedAt = DateTimeOffset.Parse("2026-05-11T23:20:00Z");
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
                    SeatId = billedSeatId,
                    OrganizationId = TestIds.OrganizationId,
                    BranchId = TestIds.BranchId,
                    ZoneId = zoneId,
                    Name = "PC-001",
                    SortOrder = 10,
                    CreatedAtUtc = now
                },
                new SeatEntity
                {
                    SeatId = guestSeatId,
                    OrganizationId = TestIds.OrganizationId,
                    BranchId = TestIds.BranchId,
                    ZoneId = zoneId,
                    Name = "PC-002",
                    SortOrder = 20,
                    CreatedAtUtc = now
                });
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = playerAccountId,
                OrganizationId = TestIds.OrganizationId,
                HomeBranchId = TestIds.BranchId,
                DisplayName = "Иван Петров",
                IsActive = true,
                CreatedAtUtc = now.AddDays(-30)
            });
            db.Tariffs.Add(new TariffEntity
            {
                TariffId = tariffId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "VIP час",
                IsActive = true,
                CreatedAtUtc = now
            });
            db.TariffVersions.Add(new TariffVersionEntity
            {
                TariffVersionId = tariffVersionId,
                TariffId = tariffId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                VersionNumber = 1,
                CurrencyCode = "TJS",
                PricePerMinuteMinorUnits = 50,
                MinimumBillableMinutes = 30,
                RoundingIncrementMinutes = 15,
                EffectiveFromUtc = now.AddDays(-1),
                CreatedAtUtc = now
            });
            db.Sessions.AddRange(
                new SessionEntity
                {
                    SessionId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
                    OrganizationId = TestIds.OrganizationId,
                    BranchId = TestIds.BranchId,
                    SeatId = billedSeatId,
                    DeviceId = Guid.NewGuid(),
                    CreatedByStaffUserId = Guid.NewGuid(),
                    PlayerKind = "account",
                    PlayerAccountId = playerAccountId,
                    TariffRuleVersionId = tariffVersionId.ToString("D"),
                    State = SessionStateNames.Active,
                    RequestedAtUtc = startedAt,
                    StartedAtUtc = startedAt,
                    EndsAtUtc = null,
                    UpdatedAtUtc = startedAt
                },
                new SessionEntity
                {
                    SessionId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
                    OrganizationId = TestIds.OrganizationId,
                    BranchId = TestIds.BranchId,
                    SeatId = guestSeatId,
                    DeviceId = Guid.NewGuid(),
                    CreatedByStaffUserId = Guid.NewGuid(),
                    PlayerKind = "guest",
                    PlayerAccountId = null,
                    TariffRuleVersionId = "guest",
                    State = SessionStateNames.Active,
                    RequestedAtUtc = startedAt,
                    StartedAtUtc = startedAt,
                    EndsAtUtc = null,
                    UpdatedAtUtc = startedAt
                });
            await db.SaveChangesAsync();
        }

        await using var readDb = new PlatformDbContext(options);
        var service = new EfFloorMapReadService(readDb, new FixedTimeProvider(now));

        var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

        Assert.NotNull(result);
        var billed = result.FloorMap.Seats.Single(seat => seat.SeatId == billedSeatId);
        Assert.Equal("Иван Петров", billed.PlayerDisplayName);
        Assert.Equal("VIP час", billed.TariffName);
        Assert.Equal(startedAt, billed.SessionStartedAtUtc);

        var guest = result.FloorMap.Seats.Single(seat => seat.SeatId == guestSeatId);
        Assert.Null(guest.PlayerDisplayName);
        Assert.Null(guest.TariffName);
        Assert.Equal(startedAt, guest.SessionStartedAtUtc);
    }

    [Fact]
    public async Task GetFloorMapAsync_ProjectsSeatGeometryZoneGeometryAndWalls()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-16T00:00:00Z");

        await using (var db = new PlatformDbContext(options))
        {
            db.Branches.Add(new BranchEntity
            {
                BranchId = TestIds.BranchId,
                OrganizationId = TestIds.OrganizationId,
                Name = "Branch",
                CreatedAtUtc = now
            });
            db.Zones.Add(new ZoneEntity
            {
                ZoneId = zoneId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "VIP",
                SortOrder = 1,
                GeoX = 1, GeoY = 2, GeoWidth = 4, GeoHeight = 3,
                Color = "#22c55e", ZoneType = "lounge",
                CreatedAtUtc = now
            });
            db.Seats.Add(new SeatEntity
            {
                SeatId = seatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "PC-1",
                SortOrder = 1,
                PosX = 3, PosY = 5, Rotation = 90, SeatType = "console",
                CreatedAtUtc = now
            });
            db.Walls.Add(new WallEntity
            {
                WallId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                X1 = 0, Y1 = 0, X2 = 10, Y2 = 0,
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new PlatformDbContext(options))
        {
            var service = new EfFloorMapReadService(db);
            var result = await service.GetFloorMapAsync(TestIds.BranchId, CancellationToken.None);

            Assert.NotNull(result);
            var seat = Assert.Single(result!.FloorMap.Seats);
            Assert.Equal(3, seat.PosX);
            Assert.Equal(5, seat.PosY);
            Assert.Equal(90, seat.Rotation);
            Assert.Equal("console", seat.SeatType);

            var zone = Assert.Single(result.FloorMap.Zones);
            Assert.Equal(4, zone.GeoWidth);
            Assert.Equal("lounge", zone.ZoneType);

            var wall = Assert.Single(result.FloorMap.Walls);
            Assert.Equal(10, wall.X2);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
