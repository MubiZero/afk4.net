using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// EF Core <see cref="IStaffPasswordResetService"/>. Issues opaque single-use reset tokens (RNG ->
/// SHA-256 stored, plaintext emailed once — mirrors <c>OpaqueStaffTokenService</c>) and, on
/// completion, rehashes the password and revokes the account's active access/refresh tokens.
/// </summary>
public sealed class EfStaffPasswordResetService(
    PlatformDbContext db,
    INotificationService notifications,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options) : IStaffPasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);

    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();
    private readonly NotificationOptions options = options.Value;

    public async Task RequestResetAsync(string userNameOrEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userNameOrEmail);

        var staff = await ResolveStaffAsync(userNameOrEmail.Trim(), cancellationToken);
        if (staff is null || string.IsNullOrWhiteSpace(staff.Email))
        {
            // Anti-enumeration: never reveal whether the account exists or has an email on file.
            return;
        }

        var now = timeProvider.GetUtcNow();
        var tokenId = Guid.NewGuid();
        var plaintext = $"{tokenId:N}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";

        db.PasswordResetTokens.Add(new PasswordResetTokenEntity
        {
            PasswordResetTokenId = tokenId,
            StaffUserId = staff.StaffUserId,
            OrganizationId = staff.OrganizationId,
            TokenHash = HashToken(plaintext),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + TokenLifetime,
        });
        await db.SaveChangesAsync(cancellationToken);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.StaffPasswordReset,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(
                Locale: options.DefaultLocale,
                EmailAddress: staff.Email,
                StaffUserId: staff.StaffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["displayName"] = staff.DisplayName,
                ["code"] = plaintext,
                ["expiresInMinutes"] = ((int)TokenLifetime.TotalMinutes).ToString(),
            },
            IdempotencyKey: $"staff-password-reset:{tokenId:N}",
            OrganizationId: staff.OrganizationId);

        await notifications.SendNowAsync(request, cancellationToken);
    }

    public async Task<bool> CompleteResetAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var now = timeProvider.GetUtcNow();
        var tokenHash = HashToken(token);

        var candidates = await db.PasswordResetTokens
            .Where(candidate => candidate.ConsumedAtUtc == null && candidate.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        var resetToken = candidates.SingleOrDefault(candidate => candidate.TokenHash.SequenceEqual(tokenHash));
        if (resetToken is null)
        {
            return false;
        }

        var staff = await db.StaffUsers.FirstOrDefaultAsync(user => user.StaffUserId == resetToken.StaffUserId, cancellationToken);
        if (staff is null)
        {
            return false;
        }

        resetToken.ConsumedAtUtc = now;
        staff.PasswordHash = passwordHasher.HashPassword(staff, newPassword);
        await StaffTokenRevocation.RevokeActiveAsync(db, staff.OrganizationId, staff.StaffUserId, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<StaffUserEntity?> ResolveStaffAsync(string userNameOrEmail, CancellationToken cancellationToken)
    {
        var normalizedUserName = userNameOrEmail.ToUpperInvariant();
        var byUserName = await db.StaffUsers
            .FirstOrDefaultAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken);
        if (byUserName is not null)
        {
            return byUserName;
        }

        var loweredEmail = userNameOrEmail.ToLowerInvariant();
        return await db.StaffUsers
            .FirstOrDefaultAsync(user => user.Email != null && user.Email.ToLower() == loweredEmail, cancellationToken);
    }

    private static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
