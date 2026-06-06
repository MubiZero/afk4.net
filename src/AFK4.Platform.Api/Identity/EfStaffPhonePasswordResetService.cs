using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// SMS password reset. Mirrors <see cref="EfStaffPhoneVerificationService"/>: a 6-digit OTP
/// (<see cref="StaffPhoneOtpPurpose.PasswordReset"/>) is sent to a verified phone, then verified to
/// set a new password. Resolves staff exactly as sign-in-by-phone (verified + active). Reuses
/// <see cref="StaffTokenRevocation"/> so a completed reset logs the account out everywhere.
/// </summary>
public sealed class EfStaffPhonePasswordResetService(
    PlatformDbContext db,
    INotificationService notifications,
    IPhoneOtpHasher hasher,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> otpOptions,
    IOptions<NotificationOptions> notificationOptions) : IStaffPhonePasswordResetService
{
    private readonly PhoneOtpOptions otpOptions = otpOptions.Value;
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;
    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();

    public async Task<ForgotPasswordByPhoneResult> RequestResetAsync(string rawPhone, CancellationToken cancellationToken)
    {
        var expiresInSeconds = (int)otpOptions.Lifetime.TotalSeconds;
        var resendAfterSeconds = (int)otpOptions.ResendCooldown.TotalSeconds;

        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new ForgotPasswordByPhoneResult(ForgotPasswordByPhoneStatus.InvalidPhone, 0, 0);
        }

        // Anti-enumeration: this exact result is returned whether or not an account exists, and
        // whether or not an SMS is actually sent (cooldown / hourly cap suppress the send silently).
        var accepted = new ForgotPasswordByPhoneResult(
            ForgotPasswordByPhoneStatus.Accepted, expiresInSeconds, resendAfterSeconds);

        var staff = await db.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.NormalizedPhone == normalizedPhone
                    && user.PhoneVerifiedAtUtc != null
                    && user.IsActive,
                cancellationToken);
        if (staff is null)
        {
            return accepted;
        }

        var now = timeProvider.GetUtcNow();

        var recent = await db.StaffPhoneOtps
            .Where(otp => otp.StaffUserId == staff.StaffUserId && otp.Purpose == StaffPhoneOtpPurpose.PasswordReset)
            .OrderByDescending(otp => otp.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null && now - recent.CreatedAtUtc < otpOptions.ResendCooldown)
        {
            return accepted;
        }

        var sinceHourAgo = now - TimeSpan.FromHours(1);
        var sendsLastHour = await db.StaffPhoneOtps.CountAsync(
            otp => otp.StaffUserId == staff.StaffUserId
                && otp.Purpose == StaffPhoneOtpPurpose.PasswordReset
                && otp.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= otpOptions.MaxSendsPerHour)
        {
            return accepted;
        }

        var code = generator.Generate();
        var otpId = Guid.NewGuid();
        db.StaffPhoneOtps.Add(new StaffPhoneOtpEntity
        {
            StaffPhoneOtpId = otpId,
            StaffUserId = staff.StaffUserId,
            OrganizationId = staff.OrganizationId,
            Phone = normalizedPhone,
            Purpose = StaffPhoneOtpPurpose.PasswordReset,
            CodeHash = hasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + otpOptions.Lifetime,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync(cancellationToken);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.StaffPasswordResetSms,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(
                Locale: notificationOptions.DefaultLocale,
                PhoneNumber: "+" + normalizedPhone,
                StaffUserId: staff.StaffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["code"] = code,
                ["expiresInMinutes"] = ((int)otpOptions.Lifetime.TotalMinutes).ToString(),
            },
            IdempotencyKey: $"staff-password-reset-sms:{otpId:N}",
            PreferredChannels: [NotificationChannel.Sms],
            OrganizationId: staff.OrganizationId);

        await notifications.SendNowAsync(request, cancellationToken);
        return accepted;
    }

    public async Task<ResetPasswordByPhoneResult> ResetAsync(
        string rawPhone, string code, string newPassword, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.NoActiveCode, 0);
        }

        var now = timeProvider.GetUtcNow();

        var staff = await db.StaffUsers
            .FirstOrDefaultAsync(
                user => user.NormalizedPhone == normalizedPhone
                    && user.PhoneVerifiedAtUtc != null
                    && user.IsActive,
                cancellationToken);
        if (staff is null)
        {
            // Anti-enumeration: behave as if there were simply no pending code.
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.NoActiveCode, 0);
        }

        var otp = await db.StaffPhoneOtps
            .Where(candidate => candidate.StaffUserId == staff.StaffUserId
                && candidate.Purpose == StaffPhoneOtpPurpose.PasswordReset
                && candidate.ConsumedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (otp is null)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.NoActiveCode, 0);
        }

        if (otp.ExpiresAtUtc <= now)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.Expired, 0);
        }

        if (otp.AttemptCount >= otpOptions.MaxAttempts)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.TooManyAttempts, 0);
        }

        var enteredDigits = PhoneOtpCode.KeepDigits(code);
        if (hasher.Hash(enteredDigits) != otp.CodeHash)
        {
            otp.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, otpOptions.MaxAttempts - otp.AttemptCount);
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.InvalidCode, remaining);
        }

        otp.ConsumedAtUtc = now;
        staff.PasswordHash = passwordHasher.HashPassword(staff, newPassword);
        await StaffTokenRevocation.RevokeActiveAsync(db, staff.OrganizationId, staff.StaffUserId, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.Success, otpOptions.MaxAttempts);
    }
}
