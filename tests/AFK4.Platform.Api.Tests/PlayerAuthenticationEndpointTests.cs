using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
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

    private static async Task<(Guid OrgId, Guid PlayerId)> SeedPlayerWithPinAsync(
        PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-06-03T00:00:00Z");

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            HomeBranchId = branchId,
            DisplayName = "Player One",
            PhoneNumber = "+992900000001",
            IsActive = true,
            CreatedAtUtc = now
        });
        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            PhoneVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        await db.SaveChangesAsync();
        return (orgId, playerId);
    }

    [Fact]
    public async Task PlayerToken_Issue_Validate_Refresh()
    {
        await using var factory = new PlatformApiFactory();
        var (orgId, playerId) = await SeedPlayerWithPinAsync(factory, "1234");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IPlayerTokenService>();
        var account = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == playerId);

        var issued = await tokenService.IssueAsync(account, true, default);
        Assert.Equal(playerId, issued.PlayerAccountId);
        Assert.True(issued.RefreshTokenExpiresAtUtc > issued.AccessTokenExpiresAtUtc);

        var ctx = await tokenService.ValidateAsync(issued.AccessToken, default);
        Assert.NotNull(ctx);
        Assert.Equal(playerId, ctx!.PlayerAccountId);
        Assert.Equal(orgId, ctx.OrganizationId);

        var refreshed = await tokenService.RefreshAsync(new PlayerRefreshRequest(issued.RefreshToken), default);
        Assert.NotNull(refreshed);
        Assert.NotEqual(issued.AccessToken, refreshed!.AccessToken);

        // old refresh token is now revoked
        Assert.Null(await tokenService.RefreshAsync(new PlayerRefreshRequest(issued.RefreshToken), default));
    }

    [Fact]
    public async Task PlayerToken_Validate_RejectsDeactivatedAccount()
    {
        await using var factory = new PlatformApiFactory();
        var (_, playerId) = await SeedPlayerWithPinAsync(factory, "1234");
        string accessToken;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IPlayerTokenService>();
            var account = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == playerId);
            accessToken = (await tokenService.IssueAsync(account, true, default)).AccessToken;
            account.IsActive = false;
            await db.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<IPlayerTokenService>();
            Assert.Null(await tokenService.ValidateAsync(accessToken, default));
        }
    }

    [Fact]
    public async Task PlayerSignIn_WrongPin_LocksAfterFiveFailures()
    {
        await using var factory = new PlatformApiFactory();
        var (orgId, playerId) = await SeedPlayerWithPinAsync(factory, "1234");
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlayerCredentialService>();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await service.SignInAsync(
                new PlayerSignInRequest(orgId, "+992900000001", "0000"), default));
        }

        var credential = await db.PlayerCredentials.SingleAsync(c => c.PlayerAccountId == playerId);
        Assert.NotNull(credential.LockedUntilUtc);

        // even the correct PIN is refused while locked
        Assert.Null(await service.SignInAsync(
            new PlayerSignInRequest(orgId, "+992900000001", "1234"), default));
    }

    [Fact]
    public async Task PlayerSignIn_CorrectPin_IssuesTokens_AndResetsFailures()
    {
        await using var factory = new PlatformApiFactory();
        var (orgId, _) = await SeedPlayerWithPinAsync(factory, "1234");
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlayerCredentialService>();

        var result = await service.SignInAsync(
            new PlayerSignInRequest(orgId, "+992900000001", "1234"), default);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.AccessToken));
    }
}
