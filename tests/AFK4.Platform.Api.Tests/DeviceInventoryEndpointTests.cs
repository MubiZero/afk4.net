using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceInventoryEndpointTests
{
    private static readonly Guid SecondDeviceId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly Guid OtherBranchId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetBranchDevices_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/devices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBranchDevices_WithCashierRole_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedDevicesAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/devices");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBranchDevices_WithTechnicianPermission_ReturnsBranchInventory()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedDevicesAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/devices");
        var devices = await response.Content.ReadFromJsonAsync<IReadOnlyList<DeviceInventoryItemDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(devices);
        Assert.Equal(2, devices.Count);
        Assert.Equal(TestIds.DeviceId, devices[0].DeviceId);
        Assert.Equal("PC-001", devices[0].MachineName);
        Assert.True(devices[0].IsOnline);
        Assert.True(devices[0].IsLocked);
        Assert.Equal("Main Hall", devices[0].ZoneName);
        Assert.Equal("Seat 01", devices[0].SeatName);
        Assert.Equal(1, devices[0].ActiveCredentialCount);
        Assert.Equal(2, devices[0].InstalledAppCount);
        Assert.Equal(1, devices[0].PendingCommandCount);
        Assert.Equal(1, devices[0].FailedCommandCount);

        Assert.Equal(SecondDeviceId, devices[1].DeviceId);
        Assert.Equal("PC-002", devices[1].MachineName);
        Assert.False(devices[1].IsOnline);
        Assert.Null(devices[1].SeatId);
    }

    private static async Task SeedDevicesAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var seatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T08:50:00Z")
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = zoneId,
            Name = "Seat 01",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T08:55:00Z")
        });
        dbContext.Devices.AddRange(
            new DeviceEntity
            {
                DeviceId = TestIds.DeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-001",
                AgentVersion = "0.1.1",
                ShellVersion = "0.1.2",
                EnrolledAtUtc = DateTimeOffset.Parse("2026-05-13T09:00:00Z"),
                LastHeartbeatAtUtc = DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
                IsOnline = true,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = SecondDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-002",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = DateTimeOffset.Parse("2026-05-13T09:10:00Z"),
                LastHeartbeatAtUtc = null,
                IsOnline = false,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = OtherBranchId,
                MachineName = "PC-OTHER",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = DateTimeOffset.Parse("2026-05-13T09:10:00Z"),
                LastHeartbeatAtUtc = null,
                IsOnline = false,
                IsLocked = true
            });
        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.Parse("33333333-3333-4333-8333-333333333333"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = seatId,
            DeviceId = TestIds.DeviceId,
            AttachedAtUtc = DateTimeOffset.Parse("2026-05-13T09:05:00Z")
        });
        dbContext.DeviceCredentials.AddRange(
            new DeviceCredentialEntity
            {
                CredentialId = Guid.Parse("44444444-4444-4444-8444-444444444444"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = TestIds.DeviceId,
                SecretHash = [1, 2, 3],
                CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T09:00:00Z")
            },
            new DeviceCredentialEntity
            {
                CredentialId = Guid.Parse("55555555-5555-4555-8555-555555555555"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = TestIds.DeviceId,
                SecretHash = [4, 5, 6],
                CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T09:01:00Z"),
                RevokedAtUtc = DateTimeOffset.Parse("2026-05-13T09:30:00Z")
            });
        dbContext.DeviceInstalledApps.AddRange(
            new DeviceInstalledAppEntity
            {
                DeviceInstalledAppId = Guid.Parse("88888888-8888-4888-8888-888888888888"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = TestIds.DeviceId,
                DisplayName = "Counter-Strike 2",
                Version = "2.0.0",
                Publisher = "Valve",
                InstallLocation = null,
                InstalledAtUtc = null,
                ReportedAtUtc = DateTimeOffset.Parse("2026-05-13T10:00:00Z")
            },
            new DeviceInstalledAppEntity
            {
                DeviceInstalledAppId = Guid.Parse("99999999-9999-4999-8999-999999999999"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = TestIds.DeviceId,
                DisplayName = "Discord",
                Version = "1.0.9059",
                Publisher = "Discord Inc.",
                InstallLocation = null,
                InstalledAtUtc = null,
                ReportedAtUtc = DateTimeOffset.Parse("2026-05-13T10:00:00Z")
            });
        dbContext.DeviceCommands.AddRange(
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("66666666-6666-4666-8666-666666666666"),
                DeviceId = TestIds.DeviceId,
                Type = "lock",
                PayloadJson = """{"reason":"operator-request"}""",
                Status = "Pending",
                CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:01:00Z"),
                UpdatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:01:00Z")
            },
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("77777777-7777-4777-8777-777777777777"),
                DeviceId = TestIds.DeviceId,
                Type = "unlock",
                PayloadJson = """{"reason":"operator-request"}""",
                Status = "Failed",
                Message = "Agent rejected command.",
                CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:02:00Z"),
                UpdatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:03:00Z")
            },
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("77777777-7777-4777-8777-777777777778"),
                DeviceId = TestIds.DeviceId,
                Type = "unlock",
                PayloadJson = """{"reason":"operator-request"}""",
                Status = "Completed",
                CreatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:04:00Z"),
                UpdatedAtUtc = DateTimeOffset.Parse("2026-05-13T10:05:00Z")
            });
        await dbContext.SaveChangesAsync();
    }
}
