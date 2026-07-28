using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceAdminEndpointTests
{
    private static readonly Guid PendingDeviceId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid OtherDeviceId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid SeatAId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SeatBId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid ZoneId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task GetPendingDevices_WithDevicePermission_ReturnsOnlyPendingDevices()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Pending);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/pending");
        var devices = await response.Content.ReadFromJsonAsync<IReadOnlyList<DeviceInventoryItemDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var device = Assert.Single(devices!);
        Assert.Equal(PendingDeviceId, device.DeviceId);
        Assert.Equal("Front desk install", device.DisplayName);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, device.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Pending, device.EnrollmentState);
        Assert.Equal(SeatAId, device.SeatId);
    }

    [Fact]
    public async Task ApprovePendingDevice_SetsEnrollmentStateAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Pending);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{PendingDeviceId:D}/approve",
            new DeviceStateChangeRequest(TestIds.OrganizationId, "Checked serial number."));
        var device = await response.Content.ReadFromJsonAsync<DeviceInventoryItemDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(device);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, device.EnrollmentState);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == PendingDeviceId);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, persisted.EnrollmentState);
        Assert.Equal(1, await dbContext.DeviceCredentials.CountAsync(credential => credential.DeviceId == PendingDeviceId && credential.RevokedAtUtc == null));

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.ApprovePendingDevice);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(PendingDeviceId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task RejectPendingDevice_RevokesCredentialAndDetachesSeat()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Pending);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{PendingDeviceId:D}/reject",
            new DeviceStateChangeRequest(TestIds.OrganizationId, "Wrong seat selected."));
        var device = await response.Content.ReadFromJsonAsync<DeviceInventoryItemDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(device);
        Assert.Equal(DeviceEnrollmentStateNames.Rejected, device.EnrollmentState);
        Assert.Null(device.SeatId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == PendingDeviceId);
        Assert.Equal(DeviceEnrollmentStateNames.Rejected, persisted.EnrollmentState);
        Assert.False(persisted.IsOnline);
        Assert.Equal(0, await dbContext.DeviceCredentials.CountAsync(credential => credential.DeviceId == PendingDeviceId && credential.RevokedAtUtc == null));
        Assert.Empty(await dbContext.DeviceSeatAssignments.Where(assignment => assignment.DeviceId == PendingDeviceId && assignment.DetachedAtUtc == null).ToListAsync());

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.RejectPendingDevice);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task RenameDevice_UpdatesDisplayNameAndKeepsMachineName()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Approved);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{PendingDeviceId:D}/rename",
            new RenameDeviceRequest(TestIds.OrganizationId, "VIP-01 replacement"));
        var device = await response.Content.ReadFromJsonAsync<DeviceInventoryItemDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(device);
        Assert.Equal("VM-PENDING", device.MachineName);
        Assert.Equal("VIP-01 replacement", device.DisplayName);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == PendingDeviceId);
        Assert.Equal("VIP-01 replacement", persisted.DisplayName);
        Assert.Equal("VM-PENDING", persisted.MachineName);
        Assert.Equal(AuditActionNames.RenameDevice, (await dbContext.AuditRecords.SingleAsync()).Action);
    }

    [Fact]
    public async Task RenameDevice_WithTooLongDisplayName_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Approved);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{PendingDeviceId:D}/rename",
            new RenameDeviceRequest(TestIds.OrganizationId, new string('D', 81)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == PendingDeviceId);
        Assert.Equal("Front desk install", persisted.DisplayName);
    }

    [Fact]
    public async Task MoveDeviceSeat_DetachesConflictingAssignmentAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Approved);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{PendingDeviceId:D}/move-seat",
            new MoveDeviceSeatRequest(TestIds.OrganizationId, SeatBId));
        var device = await response.Content.ReadFromJsonAsync<DeviceInventoryItemDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(device);
        Assert.Equal(SeatBId, device.SeatId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var activeAssignments = await dbContext.DeviceSeatAssignments
            .Where(assignment => assignment.DetachedAtUtc == null)
            .ToListAsync();
        var active = Assert.Single(activeAssignments);
        Assert.Equal(PendingDeviceId, active.DeviceId);
        Assert.Equal(SeatBId, active.SeatId);
        Assert.Equal(2, await dbContext.DeviceSeatAssignments.CountAsync(assignment => assignment.DetachedAtUtc != null));

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.MoveDeviceSeat);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task RemoveDevice_WithoutActiveSession_RemovesDeviceAndRevokesCredential()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Approved);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{PendingDeviceId:D}/remove",
            new DeviceStateChangeRequest(TestIds.OrganizationId, "PC replaced."));
        var removed = await response.Content.ReadFromJsonAsync<DeviceInventoryItemDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(removed);
        Assert.Equal(DeviceEnrollmentStateNames.Removed, removed.EnrollmentState);
        Assert.Null(removed.SeatId);

        var listResponse = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices");
        var listed = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<DeviceInventoryItemDto>>();
        Assert.DoesNotContain(listed!, device => device.DeviceId == PendingDeviceId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await dbContext.DeviceCredentials.CountAsync(credential => credential.DeviceId == PendingDeviceId && credential.RevokedAtUtc == null));
        Assert.Empty(await dbContext.DeviceSeatAssignments.Where(assignment => assignment.DeviceId == PendingDeviceId && assignment.DetachedAtUtc == null).ToListAsync());
        Assert.Equal(AuditActionNames.RemoveDevice, (await dbContext.AuditRecords.SingleAsync()).Action);
    }

    [Fact]
    public async Task RemoveDevice_WithActiveSession_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await SeedDevicesAsync(factory, pendingDeviceState: DeviceEnrollmentStateNames.Approved);
        await SeedActiveSessionAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{PendingDeviceId:D}/remove",
            new DeviceStateChangeRequest(TestIds.OrganizationId, "PC replaced."));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == PendingDeviceId);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, persisted.EnrollmentState);
        Assert.Equal(1, await dbContext.DeviceCredentials.CountAsync(credential => credential.DeviceId == PendingDeviceId && credential.RevokedAtUtc == null));
        Assert.Single(await dbContext.DeviceSeatAssignments.Where(assignment => assignment.DeviceId == PendingDeviceId && assignment.DetachedAtUtc == null).ToListAsync());
        Assert.Empty(await dbContext.AuditRecords.ToListAsync());
    }

    private static async Task SeedDevicesAsync(PlatformApiFactory factory, string pendingDeviceState)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var createdAt = DateTimeOffset.Parse("2026-05-25T09:00:00Z");

        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = createdAt
        });
        dbContext.Seats.AddRange(
            new SeatEntity
            {
                SeatId = SeatAId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-01",
                SortOrder = 1,
                CreatedAtUtc = createdAt
            },
            new SeatEntity
            {
                SeatId = SeatBId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = ZoneId,
                Name = "PC-02",
                SortOrder = 2,
                CreatedAtUtc = createdAt
            });
        dbContext.Devices.AddRange(
            new DeviceEntity
            {
                DeviceId = PendingDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "VM-PENDING",
                DisplayName = "Front desk install",
                Role = DeviceRoleNames.ManagerWorkstation,
                EnrollmentState = pendingDeviceState,
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = createdAt,
                LastHeartbeatAtUtc = createdAt.AddMinutes(2),
                IsOnline = true,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = OtherDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "VM-OTHER",
                DisplayName = "Gaming PC 02",
                Role = DeviceRoleNames.GamingPc,
                EnrollmentState = DeviceEnrollmentStateNames.Approved,
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = createdAt,
                IsOnline = false,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "VM-REMOVED",
                DisplayName = "Removed PC",
                Role = DeviceRoleNames.GamingPc,
                EnrollmentState = DeviceEnrollmentStateNames.Removed,
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = createdAt,
                IsOnline = false,
                IsLocked = true
            });
        dbContext.DeviceSeatAssignments.AddRange(
            new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = SeatAId,
                DeviceId = PendingDeviceId,
                AttachedAtUtc = createdAt.AddMinutes(1)
            },
            new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = SeatBId,
                DeviceId = OtherDeviceId,
                AttachedAtUtc = createdAt.AddMinutes(1)
            });
        dbContext.DeviceCredentials.AddRange(
            new DeviceCredentialEntity
            {
                CredentialId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = PendingDeviceId,
                SecretHash = [1, 2, 3],
                CreatedAtUtc = createdAt
            },
            new DeviceCredentialEntity
            {
                CredentialId = Guid.Parse("99999999-9999-4999-8999-999999999999"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                DeviceId = OtherDeviceId,
                SecretHash = [4, 5, 6],
                CreatedAtUtc = createdAt
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedActiveSessionAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var startedAt = DateTimeOffset.Parse("2026-05-25T09:30:00Z");
        dbContext.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.Parse("12345678-1234-4234-8234-123456789abc"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatAId,
            DeviceId = PendingDeviceId,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            TariffRuleVersionId = "manual-v1",
            State = SessionStateNames.Active,
            RequestedAtUtc = startedAt,
            StartedAtUtc = startedAt,
            EndsAtUtc = startedAt.AddHours(1),
            UpdatedAtUtc = startedAt
        });
        await dbContext.SaveChangesAsync();
    }
}
