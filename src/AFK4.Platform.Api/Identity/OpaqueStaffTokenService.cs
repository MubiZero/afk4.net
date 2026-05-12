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

    public async Task<StaffSignInResponse> IssueAsync(StaffUserEntity user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(AccessTokenLifetime);
        var token = CreateToken(Guid.NewGuid());

        dbContext.StaffAccessTokens.Add(new StaffAccessTokenEntity
        {
            StaffAccessTokenId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = user.OrganizationId,
            TokenHash = HashToken(token),
            CreatedAtUtc = now,
            ExpiresAtUtc = expiresAt
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var context = await CreateContextAsync(user, cancellationToken);

        return new StaffSignInResponse(
            StaffUserId: user.StaffUserId,
            OrganizationId: user.OrganizationId,
            DisplayName: user.DisplayName,
            AccessToken: token,
            AccessTokenExpiresAtUtc: expiresAt,
            BranchIds: context.BranchIds.OrderBy(branchId => branchId).ToArray(),
            Permissions: context.Permissions.Order(StringComparer.OrdinalIgnoreCase).ToArray());
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

    private async Task<StaffContext> CreateContextAsync(StaffUserEntity user, CancellationToken cancellationToken)
    {
        var roles = await dbContext.StaffRoleAssignments
            .AsNoTracking()
            .Where(role => role.StaffUserId == user.StaffUserId && role.OrganizationId == user.OrganizationId)
            .ToArrayAsync(cancellationToken);

        return new StaffContext(
            StaffUserId: user.StaffUserId,
            OrganizationId: user.OrganizationId,
            DisplayName: user.DisplayName,
            BranchIds: roles.Select(role => role.BranchId).ToHashSet(),
            Permissions: PermissionCatalog.GetPermissions(roles.Select(role => role.RoleName)));
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
