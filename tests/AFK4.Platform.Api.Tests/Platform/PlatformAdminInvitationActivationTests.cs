using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminInvitationActivationTests
{
    [Fact]
    public async Task ValidCode_CreatesActiveAdminWithInvitedRole()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();

        using var anonymous = factory.CreateClient();
        var accepted = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = await db.PlatformAdminUsers.SingleAsync(x => x.NormalizedUserName == "SUPPORT1");
        Assert.True(user.IsActive);
        Assert.Contains(PlatformAdminRoleNames.PlatformSupport, user.RolesJson);
    }

    [Fact]
    public async Task CodeCannotBeUsedTwice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
        using var anonymous = factory.CreateClient();
        await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

        var second = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation.Code, "support2", "Вторая поддержка", "S3cret!passphrase"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ExpiredCode_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.PlatformAdminInvitations.SingleAsync()).ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using var anonymous = factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
            new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
