using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminDirectoryPersistenceTests
{
    [Fact]
    public async Task Invitation_RoundTripsAndCodeHashIsUnique()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hash = new byte[] { 1, 2, 3, 4 };

        db.PlatformAdminInvitations.Add(new PlatformAdminInvitationEntity
        {
            InvitationId = Guid.NewGuid(),
            CodeHash = hash,
            Role = PlatformAdminRoleNames.PlatformSupport,
            Status = "pending",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(3),
            CreatedByPlatformAdminUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var stored = await db.PlatformAdminInvitations.SingleAsync();
        Assert.Equal("pending", stored.Status);
        Assert.Equal(hash, stored.CodeHash);
    }

    [Fact]
    public async Task AdminUser_HasTwoFactorColumnsWithSafeDefaults()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var user = await db.PlatformAdminUsers.FirstAsync();

        Assert.Null(user.TotpSecretEncrypted);
        Assert.Null(user.TotpEnabledAtUtc);
        Assert.Equal("[]", user.RecoveryCodeHashesJson);
        Assert.Equal(0, user.FailedTwoFactorAttempts);
    }
}
