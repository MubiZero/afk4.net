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
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsync(InstallRoutes.AuthenticatedDiscover(TestIds.OrganizationId), content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstallDiscoverResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Branches);
    }

    // Мастер ставится на каждый админский ПК, а клуб настраивают один раз: без этих признаков он
    // спрашивал бы про оформление и тариф повторно — и на тарифе с тем же именем сервер бы отказал.
    [Fact]
    public async Task AuthDiscover_TellsWhatTheClubAlreadyHas()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var fresh = await DiscoverAsync(client);
        Assert.False(fresh!.BrandingConfigured);
        Assert.All(fresh.Branches, branch => Assert.False(branch.HasTariff));
        Assert.All(fresh.Branches, branch => Assert.False(branch.HasStaffBesidesOwner));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var organization = await db.Organizations.SingleAsync(row => row.OrganizationId == TestIds.OrganizationId);
            organization.AccentColor = "#C8FF00";
            db.Tariffs.Add(new TariffEntity
            {
                TariffId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                Name = "Стандартный",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                RoleName = OrganizationRoleNames.Operator,
            });
            await db.SaveChangesAsync();
        }

        var configured = await DiscoverAsync(client);
        Assert.True(configured!.BrandingConfigured);
        var branch = Assert.Single(configured.Branches, candidate => candidate.BranchId == TestIds.BranchId);
        Assert.True(branch.HasTariff);
        Assert.True(branch.HasStaffBesidesOwner);
    }

    // Владелец есть в каждом клубе с первого дня — сам по себе он не повод пропустить шаг найма.
    [Fact]
    public async Task AuthDiscover_DoesNotCountTheOwnerAsStaff()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                RoleName = OrganizationRoleNames.OrganizationOwner,
            });
            await db.SaveChangesAsync();
        }

        var body = await DiscoverAsync(client);

        Assert.All(body!.Branches, branch => Assert.False(branch.HasStaffBesidesOwner));
    }

    private static async Task<InstallDiscoverResponse?> DiscoverAsync(HttpClient client)
    {
        var response = await client.PostAsync(InstallRoutes.AuthenticatedDiscover(TestIds.OrganizationId), content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<InstallDiscoverResponse>();
    }

    [Fact]
    public async Task AuthDiscover_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(InstallRoutes.AuthenticatedDiscover(TestIds.OrganizationId), content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthEnroll_GamingPc_AutoApproval_CreatesDeviceCredentialAndSeatAssignment()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            InstallRoutes.AuthenticatedEnroll(TestIds.OrganizationId),
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
        Assert.True(await db.DeviceSeatAssignments.AnyAsync(a => a.DeviceId == body!.DeviceId));
    }

    [Fact]
    public async Task AuthEnroll_DisplayNameTooShort_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            InstallRoutes.AuthenticatedEnroll(TestIds.OrganizationId),
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
        // CashierOperator does NOT have devices.install (confirmed in OrganizationPermissionCatalog.cs).
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            InstallRoutes.AuthenticatedEnroll(TestIds.OrganizationId),
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
