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
    private const string InvitePhone = "+992937380071";

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex CodePattern();

    private static async Task<string> ReadInviteCodeAsync(PlatformApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.NotificationOutbox.SingleAsync(r => r.TemplateKey == NotificationTemplateKeys.StaffInviteSms);
        var code = CodePattern().Match(row.BodyText).Value;
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
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(TestIds.OrganizationId, "new.cashier", "New Cashier", InvitePhone, "new.cashier@club.example",
                [OrganizationRoleNames.Operator]));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var dto = await create.Content.ReadFromJsonAsync<StaffInviteDto>();
        Assert.NotNull(dto);

        var code = await ReadInviteCodeAsync(factory);
        Assert.Equal(dto!.Code, code);

        var accept = await client.PostAsJsonAsync("/api/staff/invites/accept",
            new AcceptStaffInviteRequest(InvitePhone, code, "FreshPass123"));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await accept.Content.ReadFromJsonAsync<AcceptStaffInviteResponse>();
        Assert.Equal(TestIds.OrganizationId, accepted!.OrganizationId);
        Assert.Equal("new.cashier", accepted.UserName);

        // The invitee can now sign in with the password they chose.
        var signIn = await client.PostAsJsonAsync($"/api/organizations/{TestIds.OrganizationId:D}/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "new.cashier", "FreshPass123"));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task CreateInvite_Unauthenticated_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(TestIds.OrganizationId, "new.cashier", "New Cashier", InvitePhone, "new.cashier@club.example",
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
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/staff/invites",
            new CreateStaffInviteRequest(TestIds.OrganizationId, "tech@afk4.test", "Dup", InvitePhone, "dup@club.example",
                [OrganizationRoleNames.Operator]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    // Номер, которого никто не приглашал, отвечает тем же, чем истёкшее приглашение: разные
    // ответы превратили бы маршрут в проверялку «кого в этот клуб звали».
    public async Task Accept_ForAPhoneNobodyInvited_AnswersLikeAnExpiredCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/staff/invites/accept",
            new AcceptStaffInviteRequest(InvitePhone, "000000", "FreshPass123"));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Contains("code_expired", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
