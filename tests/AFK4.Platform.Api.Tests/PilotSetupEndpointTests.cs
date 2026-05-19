using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;

namespace AFK4.Platform.Api.Tests;

public sealed class PilotSetupEndpointTests
{
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
