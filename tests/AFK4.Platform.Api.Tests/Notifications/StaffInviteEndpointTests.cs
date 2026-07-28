using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed partial class StaffInviteEndpointTests
{
    [GeneratedRegex(@"[0-9a-fA-F]{32}\.[0-9A-Fa-f]{64}")]
    private static partial Regex TokenPattern();

    private static async Task<string> ReadInviteCodeAsync(PlatformApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.NotificationOutbox.SingleAsync(r => r.TemplateKey == NotificationTemplateKeys.StaffInvite);
        var code = TokenPattern().Match(row.BodyText).Value;
        Assert.NotEmpty(code);
        return code;
    }

    [Fact]
    public async Task CreateInviteThenAccept_OnboardsStaffEndToEnd()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var create = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(TestIds.OrganizationId, "new.cashier", "New Cashier", "new.cashier@club.example",
                [OrganizationRoleNames.Operator]));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var dto = await create.Content.ReadFromJsonAsync<StaffInviteDto>();
        Assert.NotNull(dto);

        var code = await ReadInviteCodeAsync(factory);
        Assert.Equal(dto!.Code, code);

        var accept = await client.PostAsJsonAsync("/api/staff/invites/accept",
            new AcceptStaffInviteRequest(code, "FreshPass123"));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await accept.Content.ReadFromJsonAsync<AcceptStaffInviteResponse>();
        Assert.Equal(TestIds.OrganizationId, accepted!.OrganizationId);
        Assert.Equal("new.cashier", accepted.UserName);

        // The invitee can now sign in with the password they chose.
        var signIn = await client.PostAsJsonAsync("/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "new.cashier", "FreshPass123"));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task CreateInvite_Unauthenticated_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(TestIds.OrganizationId, "new.cashier", "New Cashier", "new.cashier@club.example",
                [OrganizationRoleNames.Operator]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvite_DuplicateUserName_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        // "tech@afk4.test" is the seeded owner account — inviting the same username must fail.
        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(TestIds.OrganizationId, "tech@afk4.test", "Dup", "dup@club.example",
                [OrganizationRoleNames.Operator]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Accept_InvalidToken_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/staff/invites/accept",
            new AcceptStaffInviteRequest("00000000000000000000000000000000.deadbeef", "FreshPass123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
