using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminDirectoryEndpointTests
{
    [Fact]
    public async Task SupportRole_CannotSeeDirectory()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.GetAsync("/api/platform/admins");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SupportRole_CannotSeeDirectory_WritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.GetAsync("/api/platform/admins");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == AuditActionNames.ViewPlatformAdmins && record.Outcome == AuditOutcome.Denied)
            .SingleAsync();

        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task FullAdmin_ListsDirectoryWithSelf()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var items = await client.GetFromJsonAsync<PlatformAdminListItem[]>("/api/platform/admins");

        Assert.NotNull(items);
        Assert.Contains(items!, item => item.PlatformAdminUserId == admin.PlatformAdminId && item.IsActive);
    }

    [Fact]
    public async Task DisablingLastFullAdmin_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PatchAsJsonAsync($"/api/platform/admins/{admin.PlatformAdminId:D}",
            new UpdatePlatformAdminRequest(null, false));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_IsListedAsPendingAndRevocable()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var body = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
        var revoked = await client.PostAsync($"/api/platform/admins/invitations/{body!.Invitation.InvitationId:D}/revoke", null);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("pending", body.Invitation.Status);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
    }
}
