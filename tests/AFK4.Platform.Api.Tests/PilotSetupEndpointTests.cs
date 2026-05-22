using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class PilotSetupEndpointTests
{
    [Fact]
    public async Task BranchProfile_WithBranchManagerRole_ReadsAndUpdatesProfile()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var readResponse = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/profile");
        var readProfile = await readResponse.Content.ReadFromJsonAsync<BranchProfileDto>();

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.NotNull(readProfile);
        Assert.Equal("Demo Branch", readProfile.Name);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/profile",
            new UpdateBranchProfileRequest(TestIds.OrganizationId, "AFK4 Pilot", "Dushanbe"));
        var updatedProfile = await updateResponse.Content.ReadFromJsonAsync<BranchProfileDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedProfile);
        Assert.Equal("AFK4 Pilot", updatedProfile.Name);
        Assert.Equal("Dushanbe", updatedProfile.City);
    }

    [Fact]
    public async Task BranchProfile_WithCashierRole_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/profile",
            new UpdateBranchProfileRequest(TestIds.OrganizationId, "Blocked", "Dushanbe"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaffUser_WithOwnerRole_CreatesUserRoleAssignmentAndAllowsSignIn()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var request = new CreateStaffUserRequest(
            TestIds.OrganizationId,
            "cashier.one@afk4.test",
            "Cashier One",
            "Passw0rd!Pilot",
            [StaffRoleNames.CashierOperator]);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff",
            request);
        var staffUser = await response.Content.ReadFromJsonAsync<StaffUserDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(staffUser);
        Assert.Equal("cashier.one@afk4.test", staffUser.UserName);
        Assert.Contains(StaffRoleNames.CashierOperator, staffUser.RoleNames);

        using var signInClient = factory.CreateClient();
        var signInResponse = await signInClient.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "cashier.one@afk4.test", "Passw0rd!Pilot"));
        var signIn = await signInResponse.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        Assert.NotNull(signIn);
        Assert.Contains(TestIds.BranchId, signIn.BranchIds);
        Assert.Contains(StaffPermissionNames.StartSession, signIn.Permissions);
    }

    [Fact]
    public async Task CreateStaffUser_WithBranchManagerRole_CreatesBranchStaff()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
        var request = new CreateStaffUserRequest(
            TestIds.OrganizationId,
            "technician.one@afk4.test",
            "Technician One",
            "Passw0rd!Pilot",
            [StaffRoleNames.Technician]);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff",
            request);
        var staffUser = await response.Content.ReadFromJsonAsync<StaffUserDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(staffUser);
        Assert.Equal("technician.one@afk4.test", staffUser.UserName);
        Assert.Contains(StaffRoleNames.Technician, staffUser.RoleNames);
    }

    [Fact]
    public async Task CreateStaffUser_WithOwnerTargetRole_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var request = new CreateStaffUserRequest(
            TestIds.OrganizationId,
            "owner.two@afk4.test",
            "Owner Two",
            "Passw0rd!Pilot",
            [StaffRoleNames.Owner]);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaffUser_WithCashierRole_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var request = new CreateStaffUserRequest(
            TestIds.OrganizationId,
            "cashier.two@afk4.test",
            "Cashier Two",
            "Passw0rd!Pilot",
            [StaffRoleNames.CashierOperator]);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff",
            request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStaffRoles_WithOwnerRole_ReplacesBranchRoleAssignments()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var createResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff",
            new CreateStaffUserRequest(
                TestIds.OrganizationId,
                "roles.one@afk4.test",
                "Roles One",
                "Passw0rd!Pilot",
                [StaffRoleNames.CashierOperator]));
        var createdStaffUser = await createResponse.Content.ReadFromJsonAsync<StaffUserDto>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(createdStaffUser);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff/{createdStaffUser.StaffUserId:D}/roles",
            new UpdateStaffUserRolesRequest(
                TestIds.OrganizationId,
                [StaffRoleNames.Technician, StaffRoleNames.ShiftSupervisor]));
        var updatedStaffUser = await updateResponse.Content.ReadFromJsonAsync<StaffUserDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedStaffUser);
        Assert.DoesNotContain(StaffRoleNames.CashierOperator, updatedStaffUser.RoleNames);
        Assert.Contains(StaffRoleNames.Technician, updatedStaffUser.RoleNames);
        Assert.Contains(StaffRoleNames.ShiftSupervisor, updatedStaffUser.RoleNames);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persistedRoleNames = await dbContext.StaffRoleAssignments
            .Where(roleAssignment =>
                roleAssignment.OrganizationId == TestIds.OrganizationId &&
                roleAssignment.BranchId == TestIds.BranchId &&
                roleAssignment.StaffUserId == createdStaffUser.StaffUserId)
            .Select(roleAssignment => roleAssignment.RoleName)
            .OrderBy(roleName => roleName)
            .ToListAsync();
        Assert.Equal([StaffRoleNames.ShiftSupervisor, StaffRoleNames.Technician], persistedRoleNames);
        Assert.Contains(await dbContext.AuditRecords.ToListAsync(), audit =>
            audit.Action == AuditActionNames.UpdateStaffRoles &&
            audit.TargetId == createdStaffUser.StaffUserId.ToString("D") &&
            audit.Outcome == AuditOutcome.Succeeded);
    }

    [Fact]
    public async Task UpdateStaffRoles_WithBranchManagerRole_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff/{TestIds.TechnicianStaffUserId:D}/roles",
            new UpdateStaffUserRolesRequest(TestIds.OrganizationId, [StaffRoleNames.Technician]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LayoutSetup_WithBranchManagerRole_CreatesZoneSeatAndListsLayout()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
        var zoneResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/layout/zones",
            new CreateZoneRequest(TestIds.OrganizationId, "Main Hall", 10));
        var zone = await zoneResponse.Content.ReadFromJsonAsync<ZoneDto>();
        Assert.Equal(HttpStatusCode.OK, zoneResponse.StatusCode);
        Assert.NotNull(zone);

        var seatResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/layout/seats",
            new CreateSeatRequest(TestIds.OrganizationId, zone.ZoneId, "PC-001", 1));
        var seat = await seatResponse.Content.ReadFromJsonAsync<SeatDto>();
        Assert.Equal(HttpStatusCode.OK, seatResponse.StatusCode);
        Assert.NotNull(seat);
        Assert.Equal(zone.ZoneId, seat.ZoneId);

        var listResponse = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/layout/zones");
        var zones = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<ZoneDto>>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listedZone = Assert.Single(zones!);
        Assert.Equal("Main Hall", listedZone.Name);
        var listedSeat = Assert.Single(listedZone.Seats);
        Assert.Equal("PC-001", listedSeat.Name);
    }

    [Fact]
    public async Task CreateZone_WithCashierRole_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/layout/zones",
            new CreateZoneRequest(TestIds.OrganizationId, "Main Hall", 10));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
