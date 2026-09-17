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

    // Повторяют после обрыва связи, отказа установщика или просто по ошибке. Раньше каждый
    // повтор заводил на платформе ещё одно устройство, а место, занятое призраком, мастер
    // отказывался отдавать той самой машине, которая его и заняла.
    [Fact]
    public async Task AuthEnroll_RunAgainOnTheSameMachine_KeepsOneDeviceAndItsSeat()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var first = await EnrollAsync(client, TestIds.SeatId, DeviceRoleNames.GamingPc, "Стенд 12");
        var second = await EnrollAsync(client, TestIds.SeatId, DeviceRoleNames.GamingPc, "Стенд 12");

        Assert.Equal(HttpStatusCode.OK, second.Response.StatusCode);
        Assert.Equal(first.Body!.DeviceId, second.Body!.DeviceId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await db.Devices.CountAsync(device => device.DeviceId == first.Body.DeviceId));
        Assert.Equal(1, await db.Devices.CountAsync(device => device.BranchId == TestIds.BranchId));
        Assert.Equal(
            1,
            await db.DeviceSeatAssignments.CountAsync(assignment =>
                assignment.SeatId == TestIds.SeatId && assignment.DetachedAtUtc == null));

        // Прежний ключ отозван: повторяют в том числе потому, что предыдущий мог утечь.
        Assert.Equal(
            1,
            await db.DeviceCredentials.CountAsync(credential =>
                credential.DeviceId == first.Body.DeviceId && credential.RevokedAtUtc == null));
        Assert.NotEqual(first.Body.CredentialSecret, second.Body.CredentialSecret);
    }

    // Место занято другой машиной — это по-прежнему отказ, и понятный.
    [Fact]
    public async Task AuthEnroll_SeatTakenByAnotherMachine_IsStillRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        await EnrollAsync(client, TestIds.SeatId, DeviceRoleNames.GamingPc, "Стенд 12");
        var other = await EnrollAsync(
            client, TestIds.SeatId, DeviceRoleNames.GamingPc, "Стенд 13", publicKey: "другой-ключ");

        Assert.Equal(HttpStatusCode.Conflict, other.Response.StatusCode);
    }

    // Машину переделали из игровой в рабочее место управляющего: место обязано освободиться.
    [Fact]
    public async Task AuthEnroll_SameMachineChangingRole_FreesTheSeatItHeld()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        await EnrollAsync(client, TestIds.SeatId, DeviceRoleNames.GamingPc, "Стенд 12");
        var asWorkstation = await EnrollAsync(client, null, DeviceRoleNames.ManagerWorkstation, "Касса");

        Assert.Equal(HttpStatusCode.OK, asWorkstation.Response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.DeviceSeatAssignments.AnyAsync(assignment =>
            assignment.SeatId == TestIds.SeatId && assignment.DetachedAtUtc == null));
    }

    private static async Task<(HttpResponseMessage Response, InstallEnrollResponse? Body)> EnrollAsync(
        HttpClient client,
        Guid? seatId,
        string role,
        string displayName,
        string publicKey = "-----BEGIN PUBLIC KEY-----\nx\n-----END PUBLIC KEY-----")
    {
        var response = await client.PostAsJsonAsync(
            InstallRoutes.AuthenticatedEnroll(TestIds.OrganizationId),
            new AuthenticatedInstallEnrollRequest(
                TestIds.BranchId,
                seatId,
                role,
                displayName,
                "WIN-INSTALL-01",
                publicKey));
        var body = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<InstallEnrollResponse>()
            : null;

        return (response, body);
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
