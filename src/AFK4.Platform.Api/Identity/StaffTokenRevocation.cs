using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Revokes a staff account's active access + refresh tokens (sets <c>RevokedAtUtc</c>). Shared by
/// the email and SMS password-reset flows so a completed reset logs the account out everywhere.
/// Does NOT call SaveChanges — the caller commits within its own unit of work.
/// </summary>
internal static class StaffTokenRevocation
{
    public static async Task RevokeActiveAsync(
        PlatformDbContext db,
        Guid organizationId,
        Guid staffUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accessTokens = await db.StaffAccessTokens
            .Where(token => token.OrganizationId == organizationId && token.StaffUserId == staffUserId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in accessTokens)
        {
            token.RevokedAtUtc = now;
        }

        var refreshTokens = await db.StaffRefreshTokens
            .Where(token => token.OrganizationId == organizationId && token.StaffUserId == staffUserId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in refreshTokens)
        {
            token.RevokedAtUtc = now;
        }
    }
}
