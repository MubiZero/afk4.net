using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed class OpaquePlatformAdminTokenService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlatformAdminTokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<PlatformAdminSignInResponse> IssueAsync(
        PlatformAdminUserEntity user,
        CancellationToken cancellationToken)
    {
        return await IssueTokenPairAsync(user, cancellationToken);
    }

    public async Task<PlatformAdminSignInResponse?> RefreshAsync(
        PlatformAdminRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var refreshTokenHash = HashToken(request.RefreshToken);
        var candidates = await dbContext.PlatformAdminRefreshTokens
            .Where(candidate => candidate.RevokedAtUtc == null && candidate.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);
        var refreshToken = candidates.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(refreshTokenHash));

        if (refreshToken is null)
        {
            return null;
        }

        var user = await dbContext.PlatformAdminUsers.SingleOrDefaultAsync(
            candidate => candidate.PlatformAdminUserId == refreshToken.PlatformAdminUserId && candidate.IsActive,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        refreshToken.RevokedAtUtc = now;

        return await IssueTokenPairAsync(user, cancellationToken);
    }

    public async Task<PlatformAdminContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(bearerToken);
        var candidates = await dbContext.PlatformAdminAccessTokens
            .AsNoTracking()
            .Where(candidate => candidate.RevokedAtUtc == null && candidate.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);
        var token = candidates.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(tokenHash));

        if (token is null)
        {
            return null;
        }

        var user = await dbContext.PlatformAdminUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.PlatformAdminUserId == token.PlatformAdminUserId && candidate.IsActive,
                cancellationToken);

        return user is null
            ? null
            : CreateContext(user);
    }

    public async Task<bool> RevokeAsync(PlatformAdminSignOutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var refreshTokenHash = HashToken(request.RefreshToken);
        var candidates = await dbContext.PlatformAdminRefreshTokens
            .Where(candidate => candidate.RevokedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        var refreshToken = candidates.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(refreshTokenHash));

        if (refreshToken is null)
        {
            return false;
        }

        refreshToken.RevokedAtUtc = now;

        var matchingAccessTokens = await dbContext.PlatformAdminAccessTokens
            .Where(accessToken =>
                accessToken.PlatformAdminUserId == refreshToken.PlatformAdminUserId &&
                accessToken.RevokedAtUtc == null &&
                accessToken.CreatedAtUtc >= refreshToken.CreatedAtUtc.AddSeconds(-1) &&
                accessToken.CreatedAtUtc <= refreshToken.CreatedAtUtc.AddSeconds(1))
            .ToArrayAsync(cancellationToken);

        foreach (var accessToken in matchingAccessTokens)
        {
            accessToken.RevokedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PlatformAdminSignInResponse> IssueTokenPairAsync(
        PlatformAdminUserEntity user,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var accessTokenExpiresAt = now.Add(AccessTokenLifetime);
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);
        var accessToken = CreateToken(Guid.NewGuid());
        var refreshToken = CreateToken(Guid.NewGuid());

        dbContext.PlatformAdminAccessTokens.Add(new PlatformAdminAccessTokenEntity
        {
            PlatformAdminAccessTokenId = Guid.NewGuid(),
            PlatformAdminUserId = user.PlatformAdminUserId,
            TokenHash = HashToken(accessToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = accessTokenExpiresAt
        });
        dbContext.PlatformAdminRefreshTokens.Add(new PlatformAdminRefreshTokenEntity
        {
            PlatformAdminRefreshTokenId = Guid.NewGuid(),
            PlatformAdminUserId = user.PlatformAdminUserId,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var context = CreateContext(user);

        return new PlatformAdminSignInResponse(
            PlatformAdminId: user.PlatformAdminUserId,
            UserName: user.UserName,
            DisplayName: user.DisplayName,
            AccessToken: accessToken,
            AccessTokenExpiresAtUtc: accessTokenExpiresAt,
            RefreshToken: refreshToken,
            RefreshTokenExpiresAtUtc: refreshTokenExpiresAt,
            Roles: context.Roles.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Permissions: context.Permissions.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static PlatformAdminContext CreateContext(PlatformAdminUserEntity user)
    {
        var roles = ParseRoles(user.RolesJson);
        return new PlatformAdminContext(
            PlatformAdminUserId: user.PlatformAdminUserId,
            UserName: user.UserName,
            DisplayName: user.DisplayName,
            Roles: roles,
            Permissions: PlatformAdminPermissionCatalog.GetPermissions(roles));
    }

    internal static IReadOnlySet<string> ParseRoles(string rolesJson)
    {
        if (string.IsNullOrWhiteSpace(rolesJson))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var names = JsonSerializer.Deserialize<string[]>(rolesJson) ?? [];
            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static string SerializeRoles(IEnumerable<string> roles)
    {
        var distinct = roles
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return JsonSerializer.Serialize(distinct);
    }

    private static string CreateToken(Guid tokenId)
    {
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return $"{tokenId:N}.{secret}";
    }

    private static byte[] HashToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
