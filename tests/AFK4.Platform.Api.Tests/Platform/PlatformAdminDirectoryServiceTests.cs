using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminDirectoryServiceTests
{
    [Fact]
    public async Task DisablingLastFullAdmin_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (item, error) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest(null, false), CancellationToken.None);

        Assert.Null(item);
        Assert.Equal(PlatformAdminDirectoryError.LastFullAdmin, error);
    }

    [Fact]
    public async Task DemotingSelf_IsRejectedEvenWhenAnotherAdminExists()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformAdminUsers.Add(new PlatformAdminUserEntity
        {
            PlatformAdminUserId = Guid.NewGuid(),
            UserName = "second",
            NormalizedUserName = "SECOND",
            DisplayName = "Второй админ",
            PasswordHash = "x",
            RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([PlatformAdminRoleNames.PlatformAdmin]),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (_, error) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest(PlatformAdminRoleNames.PlatformSupport, null), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.SelfDemotion, error);
    }

    [Fact]
    public async Task Invitation_ReturnsCodeOnceAndStoresOnlyHash()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (response, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.None, error);
        Assert.False(string.IsNullOrWhiteSpace(response!.Code));
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await db.PlatformAdminInvitations.SingleAsync();
        Assert.DoesNotContain(response.Code, System.Text.Encoding.UTF8.GetString(stored.CodeHash));
    }

    [Fact]
    public async Task UnknownRole_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (_, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest("platform_god", 24), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.UnknownRole, error);
    }
}
