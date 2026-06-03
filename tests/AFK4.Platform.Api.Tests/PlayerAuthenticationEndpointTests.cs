using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class PlayerAuthenticationEndpointTests
{
    [Fact]
    public async Task PlayerCredentialEntity_RoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var playerAccountId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        db.PlayerCredentials.Add(new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = playerAccountId,
            OrganizationId = organizationId,
            PasswordHash = "hash",
            PhoneVerified = false,
            FailedLoginCount = 0,
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
        });
        await db.SaveChangesAsync();

        var loaded = await db.PlayerCredentials.SingleAsync(c => c.PlayerAccountId == playerAccountId);
        Assert.Equal("hash", loaded.PasswordHash);
        Assert.False(loaded.PhoneVerified);
        Assert.Equal(0, loaded.FailedLoginCount);
    }

    [Fact]
    public async Task PlayerAccount_MarketingOptIn_DefaultsFalse_AndRoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var id = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = id,
            OrganizationId = Guid.NewGuid(),
            HomeBranchId = Guid.NewGuid(),
            DisplayName = "Player One",
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
        });
        await db.SaveChangesAsync();

        var loaded = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == id);
        Assert.False(loaded.MarketingOptIn);

        loaded.MarketingOptIn = true;
        await db.SaveChangesAsync();
        var reloaded = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == id);
        Assert.True(reloaded.MarketingOptIn);
    }

    [Fact]
    public async Task PlayerTokenEntities_RoundTrip()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var playerAccountId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        db.PlayerAccessTokens.Add(new PlayerAccessTokenEntity
        {
            PlayerAccessTokenId = Guid.NewGuid(),
            PlayerAccountId = playerAccountId,
            OrganizationId = orgId,
            TokenHash = new byte[] { 1, 2, 3 },
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
            ExpiresAtUtc = DateTimeOffset.Parse("2026-06-03T01:00:00Z")
        });
        db.PlayerRefreshTokens.Add(new PlayerRefreshTokenEntity
        {
            PlayerRefreshTokenId = Guid.NewGuid(),
            PlayerAccountId = playerAccountId,
            OrganizationId = orgId,
            TokenHash = new byte[] { 4, 5, 6 },
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
            ExpiresAtUtc = DateTimeOffset.Parse("2026-07-03T00:00:00Z")
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PlayerAccessTokens.CountAsync(t => t.PlayerAccountId == playerAccountId));
        Assert.Equal(1, await db.PlayerRefreshTokens.CountAsync(t => t.PlayerAccountId == playerAccountId));
    }
}
