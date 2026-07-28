using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceSeatAssignmentEndpointTests
{
    private static readonly Guid SeatId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid PreviousSeatId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid OtherDeviceId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    [Fact]
    public async Task PostDeviceSeatAssignment_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{TestIds.DeviceId:D}/seat-assignment",
            new AssignDeviceSeatRequest(TestIds.OrganizationId, SeatId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceSeatAssignment_WithTechnicianPermission_AssignsDeviceAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAndDevicesAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{TestIds.DeviceId:D}/seat-assignment",
            new AssignDeviceSeatRequest(TestIds.OrganizationId, SeatId));
        var assignment = await response.Content.ReadFromJsonAsync<DeviceSeatAssignmentDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(assignment);
        Assert.Equal(TestIds.DeviceId, assignment.DeviceId);
        Assert.Equal(SeatId, assignment.SeatId);
        Assert.Null(assignment.DetachedAtUtc);

        var detail = await client.GetFromJsonAsync<DeviceDetailDto>($"/api/organizations/{TestIds.OrganizationId:D}/devices/{TestIds.DeviceId:D}");
        Assert.NotNull(detail);
        Assert.Equal(SeatId, detail.SeatId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.AssignDeviceSeat, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(assignment.DeviceSeatAssignmentId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceSeatAssignment_ReassignsByDetachingConflictingActiveAssignments()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAndDevicesAsync(factory);
        await SeedConflictingAssignmentsAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{TestIds.DeviceId:D}/seat-assignment",
            new AssignDeviceSeatRequest(TestIds.OrganizationId, SeatId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var activeAssignments = await dbContext.DeviceSeatAssignments
            .Where(assignment => assignment.DetachedAtUtc == null)
            .ToListAsync();
        var active = Assert.Single(activeAssignments);
        Assert.Equal(TestIds.DeviceId, active.DeviceId);
        Assert.Equal(SeatId, active.SeatId);

        Assert.Equal(2, await dbContext.DeviceSeatAssignments.CountAsync(assignment => assignment.DetachedAtUtc != null));
    }

    [Fact]
    public async Task PostDeviceSeatAssignment_ReplayingSameDesiredAssignment_DoesNotCreateDuplicate()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAndDevicesAsync(factory);

        var request = new AssignDeviceSeatRequest(TestIds.OrganizationId, SeatId);
        var first = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{TestIds.DeviceId:D}/seat-assignment",
            request);
        var firstAssignment = await first.Content.ReadFromJsonAsync<DeviceSeatAssignmentDto>();
        var second = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{TestIds.DeviceId:D}/seat-assignment",
            request);
        var secondAssignment = await second.Content.ReadFromJsonAsync<DeviceSeatAssignmentDto>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(firstAssignment);
        Assert.NotNull(secondAssignment);
        Assert.Equal(firstAssignment.DeviceSeatAssignmentId, secondAssignment.DeviceSeatAssignmentId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.DeviceSeatAssignments.ToListAsync());
    }

    [Fact]
    public async Task PostDeviceSeatAssignment_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedLayoutAndDevicesAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{TestIds.DeviceId:D}/seat-assignment",
            new AssignDeviceSeatRequest(TestIds.OrganizationId, SeatId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.AssignDeviceSeat, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.DeviceId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceSeatAssignment_WhenSeatOrDeviceHasActiveSession_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAndDevicesAsync(factory);
        await SeedActiveSessionAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/devices/{TestIds.DeviceId:D}/seat-assignment",
            new AssignDeviceSeatRequest(TestIds.OrganizationId, SeatId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.DeviceSeatAssignments.ToListAsync());
    }

    private static async Task SeedLayoutAndDevicesAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var createdAt = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
        var zoneId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");

        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main",
            SortOrder = 1,
            CreatedAtUtc = createdAt
        });
        dbContext.Seats.AddRange(
            new SeatEntity
            {
                SeatId = SeatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "PC-001",
                SortOrder = 1,
                CreatedAtUtc = createdAt
            },
            new SeatEntity
            {
                SeatId = PreviousSeatId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = zoneId,
                Name = "PC-OLD",
                SortOrder = 2,
                CreatedAtUtc = createdAt
            });
        dbContext.Devices.AddRange(
            new DeviceEntity
            {
                DeviceId = TestIds.DeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "VM-001",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = createdAt,
                LastHeartbeatAtUtc = createdAt,
                IsOnline = true,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = OtherDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "VM-OLD",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = createdAt,
                LastHeartbeatAtUtc = createdAt,
                IsOnline = true,
                IsLocked = true
            });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedConflictingAssignmentsAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var attachedAt = DateTimeOffset.Parse("2026-05-12T00:02:00Z");
        dbContext.DeviceSeatAssignments.AddRange(
            new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = PreviousSeatId,
                DeviceId = TestIds.DeviceId,
                AttachedAtUtc = attachedAt
            },
            new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = SeatId,
                DeviceId = OtherDeviceId,
                AttachedAtUtc = attachedAt
            });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedActiveSessionAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var startedAt = DateTimeOffset.Parse("2026-05-12T00:05:00Z");
        dbContext.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.Parse("99999999-9999-4999-8999-999999999999"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = TestIds.DeviceId,
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
