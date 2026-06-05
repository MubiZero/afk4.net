using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

public sealed class EfStaffPhoneVerificationService(
    PlatformDbContext db,
    INotificationService notifications,
    IPhoneOtpHasher hasher,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> otpOptions,
    IOptions<NotificationOptions> notificationOptions) : IStaffPhoneVerificationService
{
    private readonly PhoneOtpOptions otpOptions = otpOptions.Value;
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;

    public async Task<PhoneVerificationStartResult> StartAsync(
        Guid staffUserId, Guid organizationId, string rawPhone, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.InvalidPhone, 0, 0, null);
        }

        var now = timeProvider.GetUtcNow();

        var recent = await db.StaffPhoneOtps
            .Where(otp => otp.StaffUserId == staffUserId && otp.Purpose == StaffPhoneOtpPurpose.PhoneVerification)
            .OrderByDescending(otp => otp.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null)
        {
            var sinceLast = now - recent.CreatedAtUtc;
            if (sinceLast < otpOptions.ResendCooldown)
            {
                var wait = (int)Math.Ceiling((otpOptions.ResendCooldown - sinceLast).TotalSeconds);
                return new PhoneVerificationStartResult(PhoneVerificationStartStatus.CooldownActive, 0, wait, null);
            }
        }

        var sinceHourAgo = now - TimeSpan.FromHours(1);
        var sendsLastHour = await db.StaffPhoneOtps.CountAsync(
            otp => otp.StaffUserId == staffUserId
                && otp.Purpose == StaffPhoneOtpPurpose.PhoneVerification
                && otp.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= otpOptions.MaxSendsPerHour)
        {
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.RateLimited, 0, 0, null);
        }

        var code = generator.Generate();
        var otpId = Guid.NewGuid();
        db.StaffPhoneOtps.Add(new StaffPhoneOtpEntity
        {
            StaffPhoneOtpId = otpId,
            StaffUserId = staffUserId,
            OrganizationId = organizationId,
            Phone = normalizedPhone,
            Purpose = StaffPhoneOtpPurpose.PhoneVerification,
            CodeHash = hasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + otpOptions.Lifetime,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync(cancellationToken);

        var staff = await db.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.StaffUserId == staffUserId, cancellationToken);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.StaffPhoneVerification,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(
                Locale: notificationOptions.DefaultLocale,
                PhoneNumber: "+" + normalizedPhone,
                StaffUserId: staffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["code"] = code,
                ["expiresInMinutes"] = ((int)otpOptions.Lifetime.TotalMinutes).ToString(),
                ["displayName"] = staff?.DisplayName ?? string.Empty,
            },
            IdempotencyKey: $"staff-phone-verify:{otpId:N}",
            PreferredChannels: [NotificationChannel.Sms],
            OrganizationId: organizationId);

        var delivery = await notifications.SendNowAsync(request, cancellationToken);

        var expiresInSeconds = (int)otpOptions.Lifetime.TotalSeconds;
        var resendAfter = (int)otpOptions.ResendCooldown.TotalSeconds;
        return delivery.Delivered
            ? new PhoneVerificationStartResult(PhoneVerificationStartStatus.Sent, expiresInSeconds, resendAfter, null)
            : new PhoneVerificationStartResult(PhoneVerificationStartStatus.SmsFailed, expiresInSeconds, resendAfter, delivery.Error);
    }

    public async Task<PhoneConfirmResult> ConfirmAsync(
        Guid staffUserId, string code, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var otp = await db.StaffPhoneOtps
            .Where(candidate => candidate.StaffUserId == staffUserId
                && candidate.Purpose == StaffPhoneOtpPurpose.PhoneVerification
                && candidate.ConsumedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.NoActiveCode, 0, null);
        }

        if (otp.ExpiresAtUtc <= now)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.Expired, 0, null);
        }

        if (otp.AttemptCount >= otpOptions.MaxAttempts)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.TooManyAttempts, 0, null);
        }

        var enteredDigits = PhoneOtpCode.KeepDigits(code);
        if (hasher.Hash(enteredDigits) != otp.CodeHash)
        {
            otp.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, otpOptions.MaxAttempts - otp.AttemptCount);
            return new PhoneConfirmResult(PhoneConfirmStatus.InvalidCode, remaining, null);
        }

        // App-level uniqueness check (the DB partial-unique index is the production backstop;
        // EF InMemory doesn't enforce it, so we check here too — and it yields a clean error).
        var conflict = await db.StaffUsers.AnyAsync(
            user => user.StaffUserId != staffUserId
                && user.NormalizedPhone == otp.Phone
                && user.PhoneVerifiedAtUtc != null
                && user.IsActive,
            cancellationToken);
        if (conflict)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.PhoneAlreadyInUse, 0, null);
        }

        var staff = await db.StaffUsers.FirstOrDefaultAsync(
            user => user.StaffUserId == staffUserId, cancellationToken);
        if (staff is null)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.NoActiveCode, 0, null);
        }

        staff.Phone = "+" + otp.Phone;
        staff.NormalizedPhone = otp.Phone;
        staff.PhoneVerifiedAtUtc = now;
        otp.ConsumedAtUtc = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race against the partial-unique index.
            return new PhoneConfirmResult(PhoneConfirmStatus.PhoneAlreadyInUse, 0, null);
        }

        return new PhoneConfirmResult(PhoneConfirmStatus.Confirmed, otpOptions.MaxAttempts, staff.Phone);
    }
}

internal static class PhoneOtpCode
{
    public static string KeepDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return new string(value.Where(character => character is >= '0' and <= '9').ToArray());
    }
}
