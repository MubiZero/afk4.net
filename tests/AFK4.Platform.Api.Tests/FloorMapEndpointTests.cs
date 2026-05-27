using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.FloorMap;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class FloorMapEndpointTests
{
    [Fact]
    public async Task FloorMap_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/floor-map");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FloorMap_WithStaffWithoutFloorMapPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.AccountantAuditor);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FloorMap_WithFloorMapPermission_ReturnsPersistedSeatCards()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await response.Content.ReadFromJsonAsync<FloorMapDto>();
        Assert.NotNull(map);
        Assert.Equal("Demo Branch", map.BranchName);
        Assert.Contains(map.Seats, seat =>
            seat.SeatName == "PC-001" &&
            seat.ZoneName == "Main Hall" &&
            seat.State == "Locked" &&
            seat.DeviceId == TestIds.DeviceId);
    }

    private static async Task SeedLayoutAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow.AddMinutes(1);
        var zoneId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var seatId = Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414");

        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = now
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = zoneId,
            Name = "PC-001",
            SortOrder = 10,
            CreatedAtUtc = now
        });
        dbContext.Devices.Add(new DeviceEntity
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
        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.Parse("9a4ad2f7-b74f-4d31-a5c5-7a3d7e2e9921"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = seatId,
            DeviceId = TestIds.DeviceId,
            AttachedAtUtc = now
        });

        await dbContext.SaveChangesAsync();
    }
}
