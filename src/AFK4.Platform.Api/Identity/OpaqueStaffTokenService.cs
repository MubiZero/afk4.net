using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class OpaqueStaffTokenService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IStaffTokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(8);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<StaffSignInResponse> IssueAsync(StaffUserEntity user, CancellationToken cancellationToken)
    {
        return await IssueTokenPairAsync(user, cancellationToken);
    }

    public async Task<StaffSignInResponse?> RefreshAsync(
        StaffRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var refreshTokenHash = HashToken(request.RefreshToken);
        var candidateRefreshTokens = await dbContext.StaffRefreshTokens
            .Where(candidate =>
                candidate.OrganizationId == request.OrganizationId &&
                candidate.RevokedAtUtc == null &&
                candidate.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);
        var refreshToken = candidateRefreshTokens.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(refreshTokenHash));

        if (refreshToken is null)
        {
            return null;
        }

        var user = await dbContext.StaffUsers.SingleOrDefaultAsync(candidate =>
            candidate.StaffUserId == refreshToken.StaffUserId &&
            candidate.OrganizationId == refreshToken.OrganizationId &&
            candidate.IsActive,
            cancellationToken);

        if (user is null)
        {
            return null;
        }

        refreshToken.RevokedAtUtc = now;

        return await IssueTokenPairAsync(user, cancellationToken);
    }

    public async Task<StaffContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(bearerToken);
        var candidateTokens = await dbContext.StaffAccessTokens
            .AsNoTracking()
            .Where(candidate =>
                candidate.RevokedAtUtc == null &&
                candidate.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);
        var token = candidateTokens.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(tokenHash));

        if (token is null)
        {
            return null;
        }

        var user = await dbContext.StaffUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.StaffUserId == token.StaffUserId &&
                candidate.OrganizationId == token.OrganizationId &&
                candidate.IsActive,
                cancellationToken);

        return user is null
            ? null
            : await CreateContextAsync(user, cancellationToken);
    }

    private async Task<StaffSignInResponse> IssueTokenPairAsync(
        StaffUserEntity user,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var accessTokenExpiresAt = now.Add(AccessTokenLifetime);
        var refreshTokenExpiresAt = now.Add(RefreshTokenLifetime);
        var accessToken = CreateToken(Guid.NewGuid());
        var refreshToken = CreateToken(Guid.NewGuid());

        dbContext.StaffAccessTokens.Add(new StaffAccessTokenEntity
        {
            StaffAccessTokenId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = user.OrganizationId,
            TokenHash = HashToken(accessToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = accessTokenExpiresAt
        });
        dbContext.StaffRefreshTokens.Add(new StaffRefreshTokenEntity
        {
            StaffRefreshTokenId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = user.OrganizationId,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var context = await CreateContextAsync(user, cancellationToken);

        return new StaffSignInResponse(
            StaffUserId: user.StaffUserId,
            OrganizationId: user.OrganizationId,
            DisplayName: user.DisplayName,
            AccessToken: accessToken,
            AccessTokenExpiresAtUtc: accessTokenExpiresAt,
            RefreshToken: refreshToken,
            RefreshTokenExpiresAtUtc: refreshTokenExpiresAt,
            BranchIds: context.BranchIds.OrderBy(branchId => branchId).ToArray(),
            Permissions: context.Permissions.Order(StringComparer.OrdinalIgnoreCase).ToArray())
        {
            RoleNames = context.RoleNames
        };
    }

    private async Task<StaffContext> CreateContextAsync(StaffUserEntity user, CancellationToken cancellationToken)
    {
        var roles = await dbContext.StaffRoleAssignments
            .AsNoTracking()
            .Where(role => role.StaffUserId == user.StaffUserId && role.OrganizationId == user.OrganizationId)
            .ToArrayAsync(cancellationToken);
        var roleNames = roles
            .Select(role => role.RoleName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var byBranch = roles
            .GroupBy(role => role.BranchId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)PermissionCatalog
                    .GetPermissions(g.Select(r => r.RoleName).Distinct(StringComparer.OrdinalIgnoreCase))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));

        return new StaffContext(
            StaffUserId: user.StaffUserId,
            OrganizationId: user.OrganizationId,
            DisplayName: user.DisplayName,
            BranchIds: byBranch.Keys.ToHashSet(),
            Permissions: PermissionCatalog.GetPermissions(roleNames)
                .ToHashSet(StringComparer.OrdinalIgnoreCase))
        {
            RoleNames = roleNames,
            PermissionsByBranch = byBranch
        };
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
