using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Confirms a player's phone by SMS code — the missing half of the online top-up and booking
/// gates, which told players to have their number confirmed by the club while no code path
/// could actually set that flag.
///
/// Deliberately mirrors <see cref="EfStaffPhoneVerificationService"/> down to the option names:
/// same lifetime, cooldown, hourly cap and attempt limit. One difference is the uniqueness rule
/// — a player is identified by organization + phone, so the same number may belong to different
/// players in different clubs, and the conflict check is scoped to the organization.
/// </summary>
public sealed class EfPlayerPhoneVerificationService(
    PlatformDbContext db,
    INotificationService notifications,
    IPhoneOtpHasher hasher,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> otpOptions,
    IOptions<NotificationOptions> notificationOptions) : IPlayerPhoneVerificationService
{
    private readonly PhoneOtpOptions otpOptions = otpOptions.Value;
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;

    public async Task<PhoneVerificationStartResult> StartAsync(
        Guid playerAccountId, string rawPhone, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.InvalidPhone, 0, 0, null);
        }

        var account = await db.PlayerAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.PlayerAccountId == playerAccountId, cancellationToken);
        if (account is null)
        {
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.InvalidPhone, 0, 0, null);
        }

        var now = timeProvider.GetUtcNow();

        var recent = await db.PlayerPhoneOtps
            .Where(otp => otp.PlayerAccountId == playerAccountId
                && otp.Purpose == PlayerPhoneOtpPurpose.PhoneVerification)
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
        var sendsLastHour = await db.PlayerPhoneOtps.CountAsync(
            otp => otp.PlayerAccountId == playerAccountId
                && otp.Purpose == PlayerPhoneOtpPurpose.PhoneVerification
                && otp.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= otpOptions.MaxSendsPerHour)
        {
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.RateLimited, 0, 0, null);
        }

        var code = generator.Generate();
        var otpId = Guid.NewGuid();
        db.PlayerPhoneOtps.Add(new PlayerPhoneOtpEntity
        {
            PlayerPhoneOtpId = otpId,
            PlayerAccountId = playerAccountId,
            OrganizationId = account.OrganizationId,
            Phone = normalizedPhone,
            Purpose = PlayerPhoneOtpPurpose.PhoneVerification,
            CodeHash = hasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + otpOptions.Lifetime,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync(cancellationToken);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.PlayerPhoneVerification,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(
                Locale: account.PreferredLocale ?? notificationOptions.DefaultLocale,
                PhoneNumber: "+" + normalizedPhone,
                PlayerAccountId: playerAccountId),
            Tokens: new Dictionary<string, string>
            {
                ["code"] = code,
                ["expiresInMinutes"] = ((int)otpOptions.Lifetime.TotalMinutes).ToString(),
                ["displayName"] = account.DisplayName,
            },
            IdempotencyKey: $"player-phone-verify:{otpId:N}",
            PreferredChannels: [NotificationChannel.Sms],
            OrganizationId: account.OrganizationId);

        var delivery = await notifications.SendNowAsync(request, cancellationToken);

        var expiresInSeconds = (int)otpOptions.Lifetime.TotalSeconds;
        var resendAfter = (int)otpOptions.ResendCooldown.TotalSeconds;
        return delivery.Delivered
            ? new PhoneVerificationStartResult(PhoneVerificationStartStatus.Sent, expiresInSeconds, resendAfter, null)
            : new PhoneVerificationStartResult(
                PhoneVerificationStartStatus.SmsFailed, expiresInSeconds, resendAfter, delivery.Error);
    }

    public async Task<PhoneConfirmResult> ConfirmAsync(
        Guid playerAccountId, string code, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var otp = await db.PlayerPhoneOtps
            .Where(candidate => candidate.PlayerAccountId == playerAccountId
                && candidate.Purpose == PlayerPhoneOtpPurpose.PhoneVerification
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

        var account = await db.PlayerAccounts.FirstOrDefaultAsync(
            candidate => candidate.PlayerAccountId == playerAccountId, cancellationToken);
        if (account is null)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.NoActiveCode, 0, null);
        }

        // A number identifies a player inside one club, so the conflict is organization-scoped:
        // the same phone legitimately belongs to different players in different clubs.
        var takenByAnotherPlayer = await db.PlayerAccounts
            .Where(candidate => candidate.PlayerAccountId != playerAccountId
                && candidate.OrganizationId == account.OrganizationId
                && candidate.IsActive
                && candidate.PhoneNumber == "+" + otp.Phone)
            .Join(
                db.PlayerCredentials.Where(credential => credential.PhoneVerified),
                candidate => candidate.PlayerAccountId,
                credential => credential.PlayerAccountId,
                (candidate, _) => candidate.PlayerAccountId)
            .AnyAsync(cancellationToken);
        if (takenByAnotherPlayer)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.PhoneAlreadyInUse, 0, null);
        }

        var credential = await db.PlayerCredentials.FirstOrDefaultAsync(
            candidate => candidate.PlayerAccountId == playerAccountId, cancellationToken);
        if (credential is null)
        {
            // A player who has never set a password still owns their number: the credential row
            // is created here so the confirmed flag has somewhere to live.
            credential = new PlayerCredentialEntity
            {
                PlayerCredentialId = Guid.NewGuid(),
                PlayerAccountId = playerAccountId,
                OrganizationId = account.OrganizationId,
                CreatedAtUtc = now,
            };
            db.PlayerCredentials.Add(credential);
        }

        account.PhoneNumber = "+" + otp.Phone;
        credential.PhoneVerified = true;
        credential.PhoneVerifiedAtUtc = now;
        credential.UpdatedAtUtc = now;
        otp.ConsumedAtUtc = now;

        // Подтверждение принадлежит человеку, а не клубной карточке: прочитать код с этого
        // телефона — то же доказательство владения номером, что и на входе по коду, и там оно уже
        // поднимает флаг личности. Без этого профиль показывал бы «номер не подтверждён» сразу
        // после того, как человек его подтвердил: клубный флаг читает один маршрут, а флаг
        // личности — все остальные.
        //
        // Номер, отличный от номера личности, — это смена номера, а её волна 1 не делает: тогда
        // подтверждённым объявился бы номер, которого человек не подтверждал.
        if (account.PlatformPersonId is { } platformPersonId)
        {
            var person = await db.PlatformPersons.FirstOrDefaultAsync(
                candidate => candidate.PlatformPersonId == platformPersonId, cancellationToken);
            if (person is not null && person.PhoneNumber == account.PhoneNumber)
            {
                person.PhoneVerifiedAtUtc ??= now;
                person.UpdatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new PhoneConfirmResult(PhoneConfirmStatus.Confirmed, otpOptions.MaxAttempts, account.PhoneNumber);
    }

    public async Task<PlayerPhoneStatus> GetStatusAsync(
        Guid playerAccountId, CancellationToken cancellationToken)
    {
        var account = await db.PlayerAccounts
            .AsNoTracking()
            .Where(candidate => candidate.PlayerAccountId == playerAccountId)
            .Select(candidate => candidate.PhoneNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var verifiedAt = await db.PlayerCredentials
            .AsNoTracking()
            .Where(candidate => candidate.PlayerAccountId == playerAccountId && candidate.PhoneVerified)
            .Select(candidate => candidate.PhoneVerifiedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new PlayerPhoneStatus(account, verifiedAt);
    }
}
