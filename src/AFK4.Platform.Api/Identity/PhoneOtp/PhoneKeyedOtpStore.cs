using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity.PhoneOtp;

public enum PhoneOtpIssueStatus
{
    Issued,

    /// <summary>Код на этот номер уже уходил только что — второй он не заменяет и не удваивает.</summary>
    CooldownActive,

    /// <summary>За час с этого номера ушло столько кодов, сколько разрешено.</summary>
    RateLimited,
}

/// <summary><see cref="Code"/> и <see cref="OtpId"/> заполнены только при <see cref="PhoneOtpIssueStatus.Issued"/>.</summary>
public sealed record PhoneOtpIssueResult(PhoneOtpIssueStatus Status, string? Code, Guid? OtpId);

public enum PhoneOtpCheckStatus
{
    Confirmed,
    NoActiveCode,
    Expired,
    TooManyAttempts,
    InvalidCode,
}

public sealed record PhoneOtpCheckResult(PhoneOtpCheckStatus Status, int RemainingAttempts);

/// <summary>
/// Одноразовые коды, ключом которых служит сам номер, а не клубный счёт. У человека, скачавшего
/// приложение дома, счёта ещё нет, а лимит на отправку ему нужен ровно такой же: без него любой
/// незнакомый номер получал бы неограниченную рассылку SMS за счёт клубов.
///
/// Настройки — те же <see cref="PhoneOtpOptions"/>, что и у входа по коду: второй набор порогов
/// разъехался бы с первым на первом же исправлении.
/// </summary>
public sealed class PhoneKeyedOtpStore(
    PlatformDbContext db,
    IPhoneOtpHasher hasher,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> options)
{
    private readonly PhoneOtpOptions options = options.Value;

    public int ExpiresInSeconds => (int)this.options.Lifetime.TotalSeconds;

    public int ResendAfterSeconds => (int)this.options.ResendCooldown.TotalSeconds;

    public int ExpiresInMinutes => (int)this.options.Lifetime.TotalMinutes;

    /// <param name="normalizedPhone">Номер в форме <see cref="PhoneNumberNormalizer"/> — только цифры.</param>
    public async Task<PhoneOtpIssueResult> IssueAsync(
        string normalizedPhone, PlatformPhoneOtpPurpose purpose, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var recent = await db.PlatformPhoneOtps
            .Where(otp => otp.Phone == normalizedPhone && otp.Purpose == purpose)
            .OrderByDescending(otp => otp.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null && now - recent.CreatedAtUtc < options.ResendCooldown)
        {
            return new PhoneOtpIssueResult(PhoneOtpIssueStatus.CooldownActive, null, null);
        }

        var sinceHourAgo = now - TimeSpan.FromHours(1);
        var sendsLastHour = await db.PlatformPhoneOtps.CountAsync(
            otp => otp.Phone == normalizedPhone
                && otp.Purpose == purpose
                && otp.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= options.MaxSendsPerHour)
        {
            return new PhoneOtpIssueResult(PhoneOtpIssueStatus.RateLimited, null, null);
        }

        var code = generator.Generate();
        var otpId = Guid.NewGuid();
        db.PlatformPhoneOtps.Add(new PlatformPhoneOtpEntity
        {
            PlatformPhoneOtpId = otpId,
            Phone = normalizedPhone,
            Purpose = purpose,
            CodeHash = hasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + options.Lifetime,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync(cancellationToken);

        return new PhoneOtpIssueResult(PhoneOtpIssueStatus.Issued, code, otpId);
    }

    /// <summary>Проверяет код и, если он верен, гасит его: второй раз тем же кодом не входят.</summary>
    public async Task<PhoneOtpCheckResult> ConsumeAsync(
        string normalizedPhone,
        PlatformPhoneOtpPurpose purpose,
        string code,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var otp = await db.PlatformPhoneOtps
            .Where(candidate => candidate.Phone == normalizedPhone
                && candidate.Purpose == purpose
                && candidate.ConsumedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return new PhoneOtpCheckResult(PhoneOtpCheckStatus.NoActiveCode, 0);
        }

        if (otp.ExpiresAtUtc <= now)
        {
            return new PhoneOtpCheckResult(PhoneOtpCheckStatus.Expired, 0);
        }

        if (otp.AttemptCount >= options.MaxAttempts)
        {
            return new PhoneOtpCheckResult(PhoneOtpCheckStatus.TooManyAttempts, 0);
        }

        if (hasher.Hash(PhoneOtpCode.KeepDigits(code)) != otp.CodeHash)
        {
            otp.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            return new PhoneOtpCheckResult(
                PhoneOtpCheckStatus.InvalidCode, Math.Max(0, options.MaxAttempts - otp.AttemptCount));
        }

        otp.ConsumedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return new PhoneOtpCheckResult(PhoneOtpCheckStatus.Confirmed, options.MaxAttempts);
    }
}
