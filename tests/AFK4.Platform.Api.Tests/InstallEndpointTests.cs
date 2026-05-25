using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
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

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == body.DeviceId);
        Assert.Equal("PC-101", device.MachineName);
        Assert.Equal("PC-101", device.DisplayName);
        Assert.Equal(DeviceRoleNames.GamingPc, device.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, device.EnrollmentState);
        Assert.NotNull(device.EnrolledViaOwnerCodeId);

        var assignment = await dbContext.DeviceSeatAssignments.SingleAsync(candidate => candidate.DeviceId == body.DeviceId);
        Assert.Equal(TestIds.SeatId, assignment.SeatId);
        Assert.Null(assignment.DetachedAtUtc);

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.InstallEnrollSucceeded);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task Enroll_WhenBranchRequiresManualApproval_CreatesPendingDevice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var ownerCode = await GenerateOwnerCodeAsync(client);
        await SeedLayoutAsync(factory, requireManualApproval: true);
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync(
            "/api/install/enroll",
            new InstallEnrollRequest(
                ownerCode,
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.ManagerWorkstation,
                "Manager desk",
                "MANAGER-01",
                "device-public-key"));
        var body = await response.Content.ReadFromJsonAsync<InstallEnrollResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(DeviceEnrollmentStateNames.Pending, body.EnrollmentState);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await dbContext.Devices.SingleAsync(candidate => candidate.DeviceId == body.DeviceId);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, device.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Pending, device.EnrollmentState);
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
