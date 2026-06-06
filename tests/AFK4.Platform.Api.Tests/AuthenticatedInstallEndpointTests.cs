using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class AuthenticatedInstallEndpointTests
{
    [Fact]
    public async Task AuthDiscover_AsTechnician_ReturnsAssignedBranches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsync("/api/install/auth/discover", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstallDiscoverResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Branches);
    }

    [Fact]
    public async Task AuthDiscover_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/install/auth/discover", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthEnroll_GamingPc_AutoApproval_CreatesDeviceCredentialAndSeatAssignment()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/install/auth/enroll",
            new AuthenticatedInstallEnrollRequest(
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                "Стенд 12",
                "WIN-INSTALL-01",
                "-----BEGIN PUBLIC KEY-----\nx\n-----END PUBLIC KEY-----"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstallEnrollResponse>();
        Assert.NotNull(body);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await db.Devices.SingleAsync(d => d.DeviceId == body!.DeviceId);
        Assert.Null(device.EnrolledViaOwnerCodeId);
        Assert.True(await db.DeviceSeatAssignments.AnyAsync(a => a.DeviceId == body!.DeviceId));
    }

    [Fact]
    public async Task AuthEnroll_DisplayNameTooShort_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/install/auth/enroll",
            new AuthenticatedInstallEnrollRequest(
                TestIds.BranchId,
                null,
                DeviceRoleNames.ManagerWorkstation,
                "ab",
                "WIN-INSTALL-02",
                "-----BEGIN PUBLIC KEY-----\nx\n-----END PUBLIC KEY-----"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthEnroll_WithoutInstallPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // CashierOperator does NOT have devices.install (confirmed in PermissionCatalog.cs).
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/install/auth/enroll",
            new AuthenticatedInstallEnrollRequest(
                TestIds.BranchId,
                TestIds.SeatId,
                DeviceRoleNames.GamingPc,
                "Стенд 1",
                "WIN-INSTALL-03",
                "-----BEGIN PUBLIC KEY-----\nx\n-----END PUBLIC KEY-----"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedLayoutAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branch = await dbContext.Branches.SingleAsync(b => b.BranchId == TestIds.BranchId);
        branch.Slug = "demo";
        branch.RequireManualDeviceApproval = false;

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
}
