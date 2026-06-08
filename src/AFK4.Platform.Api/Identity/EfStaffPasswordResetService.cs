using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// EF Core <see cref="IStaffPasswordResetService"/>. The email channel mirrors the SMS channel
/// (<see cref="EfStaffPhonePasswordResetService"/>): a 6-digit OTP is emailed and stored hashed in
/// <see cref="PasswordResetTokenEntity"/>, then verified (resolved by login/email) with an attempt
/// counter. On success the password is rehashed and the account's active sessions are revoked.
/// Reuses the SMS abuse limits (<see cref="PhoneOtpOptions"/>) but a longer email lifetime, since
/// email delivery is slower than SMS.
/// </summary>
public sealed class EfStaffPasswordResetService(
    PlatformDbContext db,
    INotificationService notifications,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> otpOptions,
    IOptions<NotificationOptions> options) : IStaffPasswordResetService
{
    // Email delivery is slower than SMS, so the emailed code lives longer than the 5-minute SMS one.
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();
    private readonly PhoneOtpOptions otpOptions = otpOptions.Value;
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

        // Same abuse limits as the SMS channel: a resend cooldown and an hourly send cap.
        var recent = await db.PasswordResetTokens
            .Where(token => token.StaffUserId == staff.StaffUserId)
            .OrderByDescending(token => token.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null && now - recent.CreatedAtUtc < otpOptions.ResendCooldown)
        {
            return;
        }

        var sinceHourAgo = now - TimeSpan.FromHours(1);
        var sendsLastHour = await db.PasswordResetTokens.CountAsync(
            token => token.StaffUserId == staff.StaffUserId && token.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= otpOptions.MaxSendsPerHour)
        {
            return;
        }

        var code = generator.Generate();
        var tokenId = Guid.NewGuid();
        db.PasswordResetTokens.Add(new PasswordResetTokenEntity
        {
            PasswordResetTokenId = tokenId,
            StaffUserId = staff.StaffUserId,
            OrganizationId = staff.OrganizationId,
            TokenHash = HashCode(code),
            AttemptCount = 0,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + CodeLifetime,
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
                ["code"] = code,
                ["expiresInMinutes"] = ((int)CodeLifetime.TotalMinutes).ToString(),
            },
            IdempotencyKey: $"staff-password-reset:{tokenId:N}",
            OrganizationId: staff.OrganizationId);

        await notifications.SendNowAsync(request, cancellationToken);
    }

    public async Task<ResetPasswordByEmailResult> ResetAsync(
        string userNameOrEmail, string code, string newPassword, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userNameOrEmail);
        ArgumentNullException.ThrowIfNull(code);

        var now = timeProvider.GetUtcNow();

        var staff = await ResolveStaffAsync(userNameOrEmail.Trim(), cancellationToken);
        if (staff is null)
        {
            // Anti-enumeration: behave as if there were simply no pending code.
            return new ResetPasswordByEmailResult(ResetPasswordByEmailStatus.NoActiveCode, 0);
        }

        var token = await db.PasswordResetTokens
            .Where(candidate => candidate.StaffUserId == staff.StaffUserId && candidate.ConsumedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null)
        {
            return new ResetPasswordByEmailResult(ResetPasswordByEmailStatus.NoActiveCode, 0);
        }

        if (token.ExpiresAtUtc <= now)
        {
            return new ResetPasswordByEmailResult(ResetPasswordByEmailStatus.Expired, 0);
        }

        if (token.AttemptCount >= otpOptions.MaxAttempts)
        {
            return new ResetPasswordByEmailResult(ResetPasswordByEmailStatus.TooManyAttempts, 0);
        }

        var enteredDigits = PhoneOtpCode.KeepDigits(code);
        if (!token.TokenHash.SequenceEqual(HashCode(enteredDigits)))
        {
            token.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, otpOptions.MaxAttempts - token.AttemptCount);
            return new ResetPasswordByEmailResult(ResetPasswordByEmailStatus.InvalidCode, remaining);
        }

        token.ConsumedAtUtc = now;
        staff.PasswordHash = passwordHasher.HashPassword(staff, newPassword);
        await StaffTokenRevocation.RevokeActiveAsync(db, staff.OrganizationId, staff.StaffUserId, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new ResetPasswordByEmailResult(ResetPasswordByEmailStatus.Success, otpOptions.MaxAttempts);
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

    private static byte[] HashCode(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));
}
