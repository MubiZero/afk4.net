using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class InstallEndpointTests
{
    [Fact]
    public async Task Discover_WithOwnerCode_ReturnsOnlyOwnerOrganizationBranchesAndFreeSeats()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        await SeedOtherOrganizationLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/discover",
            new InstallDiscoverRequest(ownerCode));
        var body = await response.Content.ReadFromJsonAsync<InstallDiscoverResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Tech One", body.OwnerDisplayName);
        var branch = Assert.Single(body.Branches);
        Assert.Equal(TestIds.BranchId, branch.BranchId);
        Assert.Equal("demo", branch.Slug);
        Assert.Contains(TestIds.SeatId, branch.FreeSeatIds);
        Assert.DoesNotContain(TestIds.OtherBranchId, body.Branches.Select(candidate => candidate.BranchId));
    }

    [Fact]
    public async Task Enroll_WithOwnerCodeAndAutoApproval_CreatesApprovedDeviceCredentialAndSeatAssignment()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10, 10.0.0.5");

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new InstallEnrollRequest(
                ownerCode,
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                "PC-101",
                "PC-101",
                "device-public-key"));
        var body = await response.Content.ReadFromJsonAsync<InstallEnrollResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.Equal(TestIds.BranchId, body.BranchId);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, body.EnrollmentState);
        Assert.NotEqual(Guid.Empty, body.DeviceId);
        Assert.NotEqual(Guid.Empty, body.CredentialId);
        Assert.False(string.IsNullOrWhiteSpace(body.CredentialSecret));
        Assert.Equal("test-lease-public-key", body.LeaseSigningPublicKeyPem);
        Assert.Equal("test-update-public-key", body.UpdatePackageSigningPublicKeyPem);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == body.DeviceId);
        Assert.Equal("PC-101", device.MachineName);
        Assert.Equal("PC-101", device.DisplayName);
        Assert.Equal("device-public-key", device.DevicePublicKey);
        Assert.Equal(DeviceRoleNames.GamingPc, device.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, device.EnrollmentState);
        Assert.NotNull(device.EnrolledViaOwnerCodeId);

        var assignment = await dbContext.DeviceSeatAssignments.SingleAsync(candidate => candidate.DeviceId == body.DeviceId);
        Assert.Equal(TestIds.SeatId, assignment.SeatId);
        Assert.Null(assignment.DetachedAtUtc);

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.InstallEnrollSucceeded);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Contains("203.0.113.10", audit.DetailsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enroll_ManagerWorkstationWithoutSeat_CreatesApprovedDeviceWithoutSeatAssignment()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerAuthorization = client.DefaultRequestHeaders.Authorization;
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new
            {
                OwnerCode = ownerCode,
                BranchId = TestIds.BranchId,
                SeatId = (Guid?)null,
                Role = DeviceRoleNames.ManagerWorkstation,
                DisplayName = "Manager desk",
                MachineName = "MANAGER-01",
                DevicePublicKey = "device-public-key"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstallEnrollResponse>();
        Assert.NotNull(body);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, body.EnrollmentState);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == body.DeviceId);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, device.Role);
        Assert.Equal("Manager desk", device.DisplayName);
        Assert.Empty(await dbContext.DeviceSeatAssignments.Where(assignment => assignment.DeviceId == body.DeviceId).ToListAsync());

        client.DefaultRequestHeaders.Authorization = ownerAuthorization;
        var floorMapResponse = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");
        var floorMap = await floorMapResponse.Content.ReadFromJsonAsync<FloorMapDto>();
        Assert.Equal(HttpStatusCode.OK, floorMapResponse.StatusCode);
        Assert.NotNull(floorMap);
        var seat = Assert.Single(floorMap.Seats, candidate => candidate.SeatId == TestIds.SeatId);
        Assert.Null(seat.DeviceId);
    }

    [Fact]
    public async Task Enroll_WhenBranchRequiresManualApproval_CreatesPendingDevice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerAuthorization = client.DefaultRequestHeaders.Authorization;
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory, requireManualApproval: true);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new
            {
                OwnerCode = ownerCode,
                BranchId = TestIds.BranchId,
                SeatId = (Guid?)null,
                Role = DeviceRoleNames.ManagerWorkstation,
                DisplayName = "Manager desk",
                MachineName = "MANAGER-01",
                DevicePublicKey = "device-public-key"
            });
        var body = await response.Content.ReadFromJsonAsync<InstallEnrollResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(DeviceEnrollmentStateNames.Pending, body.EnrollmentState);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == body.DeviceId);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, device.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Pending, device.EnrollmentState);

        var validator = scope.ServiceProvider.GetRequiredService<IDeviceCredentialValidator>();
        Assert.True(validator.Validate(TestIds.OrganizationId, TestIds.BranchId, body.DeviceId, body.CredentialSecret));
        Assert.False(validator.ValidateApproved(TestIds.OrganizationId, TestIds.BranchId, body.DeviceId, body.CredentialSecret));

        dbContext.DeviceCommands.Add(new DeviceCommandEntity
        {
            DeviceId = body.DeviceId,
            CommandId = Guid.Parse("77777777-7777-4777-8777-777777777777"),
            Type = DeviceCommandTypeNames.Unlock,
            PayloadJson = """{"reason":"pending-device-regression"}""",
            Status = "Pending",
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-25T11:30:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-05-25T11:30:00Z")
        });
        await dbContext.SaveChangesAsync();

        using var heartbeat = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{body.DeviceId:D}/heartbeat")
        {
            Content = JsonContent.Create(new DeviceHeartbeatRequest(
                TestIds.OrganizationId,
                TestIds.BranchId,
                body.DeviceId,
                "MANAGER-01",
                "0.1.0",
                "0.1.0",
                DateTimeOffset.Parse("2026-05-25T11:31:00Z"),
                IsLocked: true,
                ActiveSessionId: null,
                ActiveSessionLeaseExpiresAtUtc: null,
                ActiveSessionLeaseSequence: null))
        };
        heartbeat.Headers.Add(DeviceCredentialHeaders.CredentialSecret, body.CredentialSecret);
        var heartbeatResponse = await client.SendAsync(heartbeat);
        var heartbeatBody = await heartbeatResponse.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.Equal(HttpStatusCode.OK, heartbeatResponse.StatusCode);
        Assert.NotNull(heartbeatBody);
        Assert.Empty(heartbeatBody.Commands);

        client.DefaultRequestHeaders.Authorization = ownerAuthorization;
        var startResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                TestIds.SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 30,
                TariffRuleVersionId: "manual-v1",
                IdempotencyKey: "pending-device-start"));
        Assert.Equal(HttpStatusCode.BadRequest, startResponse.StatusCode);

        var commandResponse = await client.PostAsJsonAsync(
            $"/api/devices/{body.DeviceId:D}/commands",
            new CreateDeviceCommandRequest(
                DeviceCommandTypeNames.Unlock,
                new Dictionary<string, string> { ["reason"] = "pending-device-regression" }));
        Assert.Equal(HttpStatusCode.Conflict, commandResponse.StatusCode);

        var floorMapResponse = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");
        var floorMap = await floorMapResponse.Content.ReadFromJsonAsync<FloorMapDto>();
        Assert.Equal(HttpStatusCode.OK, floorMapResponse.StatusCode);
        Assert.NotNull(floorMap);
        var seat = Assert.Single(floorMap.Seats, candidate => candidate.SeatId == TestIds.SeatId);
        Assert.Equal("Maintenance", seat.State);
        Assert.Null(seat.DeviceId);
        Assert.Null(seat.IsDeviceOnline);
    }

    [Fact]
    public async Task Enroll_WithTooLongDisplayName_ReturnsBadRequestBeforePersistence()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new InstallEnrollRequest(
                ownerCode,
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                new string('D', 81),
                "PC-102",
                "device-public-key"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.Devices.Where(device => device.MachineName == "PC-102").ToListAsync());
    }

    [Fact]
    public async Task Enroll_WithTooLongMachineName_ReturnsBadRequestBeforePersistence()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new InstallEnrollRequest(
                ownerCode,
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                "PC-102",
                new string('M', 129),
                "device-public-key"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.Devices.ToListAsync());
    }

    [Fact]
    public async Task Enroll_WithoutDevicePublicKey_ReturnsBadRequestBeforePersistence()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new InstallEnrollRequest(
                ownerCode,
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                "PC-102",
                "PC-102",
                "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.Devices.ToListAsync());
    }

    [Fact]
    public async Task Discover_WithSuspendedTenant_ReturnsBadRequestWithoutBranches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var organization = await dbContext.Organizations.SingleAsync(candidate => candidate.OrganizationId == TestIds.OrganizationId);
            organization.Status = "suspended";
            organization.StatusReason = "billing hold";
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/discover",
            new InstallDiscoverRequest(ownerCode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_WithRevokedOwnerCode_ReturnsBadRequestAndAuditsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var revokedCode = await GenerateOwnerCodeAsync(client);
        await client.PostAsJsonAsync("/api/staff/me/owner-code/rotate", new RotateOwnerCodeRequest("test-revocation"));
        await SeedLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new InstallEnrollRequest(
                revokedCode,
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                "PC-102",
                "PC-102",
                "device-public-key"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.InstallEnrollRejected);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task Enroll_WithFiveResolvedOwnerCodeFailures_RevokesActiveOwnerCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        await SeedOtherOrganizationLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/install/enroll",
                new InstallEnrollRequest(
                    ownerCode,
                    TestIds.OtherBranchId,
                    TestIds.SeatId,
                    DeviceRoleNames.GamingPc,
                    "PC-102",
                    "PC-102",
                    "device-public-key"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.OwnerCodes.SingleAsync();
        Assert.Equal(5, stored.FailedAttemptCount);
        Assert.NotNull(stored.RevokedAtUtc);
        Assert.Equal("brute_force_detected", stored.RevokedReason);
    }

    [Fact]
    public async Task CreateSeat_WithOwnerCode_CreatesFreeSeatInOwnerBranchAndAuditsSuccess()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/seats",
            new InstallCreateSeatRequest(ownerCode, TestIds.BranchId, TestIds.ZoneId, "PC-102"));
        var body = await response.Content.ReadFromJsonAsync<InstallCreateSeatResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.Equal(TestIds.BranchId, body.BranchId);
        Assert.Equal(TestIds.ZoneId, body.ZoneId);
        Assert.Equal("PC-102", body.Name);
        Assert.Equal(2, body.SortOrder);

        var discover = await client.PostAsJsonAsync(
            "/api/install/discover",
            new InstallDiscoverRequest(ownerCode));
        var discoverBody = await discover.Content.ReadFromJsonAsync<InstallDiscoverResponse>();
        Assert.Equal(HttpStatusCode.OK, discover.StatusCode);
        Assert.NotNull(discoverBody);
        var branch = Assert.Single(discoverBody.Branches);
        Assert.Contains(body.SeatId, branch.FreeSeatIds);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var seat = await dbContext.Seats.SingleAsync(candidate => candidate.SeatId == body.SeatId);
        Assert.Equal("PC-102", seat.Name);
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.CreateSeat);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Contains(body.SeatId.ToString("D"), audit.TargetId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSeat_WithBranchOutsideOwnerOrganization_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory);
        await SeedOtherOrganizationLayoutAsync(factory);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/seats",
            new InstallCreateSeatRequest(ownerCode, TestIds.OtherBranchId, TestIds.ZoneId, "PC-OTHER"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.Seats.Where(seat => seat.Name == "PC-OTHER").ToListAsync());
    }

    private static async Task<string> GenerateOwnerCodeAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/staff/me/owner-code/generate", content: null);
        var body = await response.Content.ReadFromJsonAsync<OwnerCodeIssuedResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body.OwnerCode;
    }

    private static async Task SeedLayoutAsync(PlatformApiFactory factory, bool requireManualApproval = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branch = await dbContext.Branches.SingleAsync(candidate => candidate.BranchId == TestIds.BranchId);
        branch.Slug = "demo";
        branch.RequireManualDeviceApproval = requireManualApproval;

        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = TestIds.ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main hall",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = TestIds.SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = TestIds.ZoneId,
            Name = "PC-101",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedOtherOrganizationLayoutAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OtherOrganizationId,
            Slug = "other-org",
            Name = "Other Org",
            Status = "active",
            PlanCode = "pilot",
            SubscriptionStatus = "trialing",
            LimitsJson = "{}",
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.OtherBranchId,
            OrganizationId = TestIds.OtherOrganizationId,
            Slug = "main",
            Name = "Other Branch",
            City = "Other City",
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        await dbContext.SaveChangesAsync();
    }
}
