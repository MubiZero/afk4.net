using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class OpaquePlayerTokenService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlayerTokenService
{
    // Shorter access lifetime than staff (8h) — customer devices are less trusted.
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<PlayerSignInResponse> IssueAsync(
        PlayerAccountEntity account, bool phoneVerified, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var accessToken = CreateToken();
        var refreshToken = CreateToken();
        var accessExpires = now.Add(AccessTokenLifetime);
        var refreshExpires = now.Add(RefreshTokenLifetime);

        dbContext.PlayerAccessTokens.Add(new PlayerAccessTokenEntity
        {
            PlayerAccessTokenId = Guid.NewGuid(),
            PlayerAccountId = account.PlayerAccountId,
            OrganizationId = account.OrganizationId,
            TokenHash = HashToken(accessToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = accessExpires
        });
        dbContext.PlayerRefreshTokens.Add(new PlayerRefreshTokenEntity
        {
            PlayerRefreshTokenId = Guid.NewGuid(),
            PlayerAccountId = account.PlayerAccountId,
            OrganizationId = account.OrganizationId,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlayerSignInResponse(
            account.PlayerAccountId,
            account.OrganizationId,
            account.DisplayName,
            phoneVerified,
            accessToken,
            accessExpires,
            refreshToken,
            refreshExpires);
    }

    public async Task<PlayerSignInResponse?> RefreshAsync(
        PlayerRefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var hash = HashToken(request.RefreshToken);
        var candidates = await dbContext.PlayerRefreshTokens
            .Where(token => token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);
        var stored = candidates.SingleOrDefault(token => token.TokenHash.SequenceEqual(hash));

        if (stored is null)
        {
            return null;
        }

        var account = await dbContext.PlayerAccounts
            .SingleOrDefaultAsync(p => p.PlayerAccountId == stored.PlayerAccountId, cancellationToken);
        if (account is null || !account.IsActive)
        {
            return null;
        }

        stored.RevokedAtUtc = now;
        var credential = await dbContext.PlayerCredentials
            .SingleOrDefaultAsync(c => c.PlayerAccountId == account.PlayerAccountId, cancellationToken);
        return await IssueAsync(account, credential?.PhoneVerified ?? false, cancellationToken);
    }

    public async Task<PlayerContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var hash = HashToken(bearerToken);
        var candidates = await dbContext.PlayerAccessTokens
            .Where(token => token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);
        var stored = candidates.SingleOrDefault(token => token.TokenHash.SequenceEqual(hash));

        if (stored is null)
        {
            return null;
        }

        var credential = await dbContext.PlayerCredentials
            .SingleOrDefaultAsync(c => c.PlayerAccountId == stored.PlayerAccountId, cancellationToken);

        return new PlayerContext(stored.PlayerAccountId, stored.OrganizationId, credential?.PhoneVerified ?? false);
    }

    private static string CreateToken()
    {
        var tokenId = Guid.NewGuid();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return $"{tokenId:N}.{secret}";
    }

    private static byte[] HashToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
