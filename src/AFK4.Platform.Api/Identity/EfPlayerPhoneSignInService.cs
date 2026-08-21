using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Sign-in by SMS code. The phone already identifies a player inside a club, so a code proves
/// exactly what a PIN proves — with nothing to forget and nothing to reset.
///
/// The start endpoint answers identically for known and unknown numbers. Timings that differed
/// would let anyone probe which phone plays in which club, which is precisely the fact the
/// player-facing API must not hand out.
/// </summary>
public sealed class EfPlayerPhoneSignInService(
    PlatformDbContext db,
    INotificationService notifications,
    IPlayerTokenService tokenService,
    IPlatformPersonTokenService personTokenService,
    IPhoneOtpHasher hasher,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> otpOptions,
    IOptions<NotificationOptions> notificationOptions) : IPlayerPhoneSignInService
{
    private readonly PhoneOtpOptions otpOptions = otpOptions.Value;
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;

    public async Task<PhoneVerificationStartResult> StartAsync(
        Guid organizationId, string rawPhone, CancellationToken cancellationToken)
    {
        var expiresInSeconds = (int)otpOptions.Lifetime.TotalSeconds;
        var resendAfter = (int)otpOptions.ResendCooldown.TotalSeconds;
        var sent = new PhoneVerificationStartResult(
            PhoneVerificationStartStatus.Sent, expiresInSeconds, resendAfter, null);

        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            // A malformed number cannot belong to anyone, so saying so leaks nothing.
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.InvalidPhone, 0, 0, null);
        }

        var account = await db.PlayerAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId
                    && candidate.IsActive
                    && candidate.PhoneNumber == "+" + normalizedPhone,
                cancellationToken);
        if (account is null)
        {
            return sent;
        }

        var now = timeProvider.GetUtcNow();

        var recent = await db.PlayerPhoneOtps
            .Where(otp => otp.PlayerAccountId == account.PlayerAccountId
                && otp.Purpose == PlayerPhoneOtpPurpose.SignIn)
            .OrderByDescending(otp => otp.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null && now - recent.CreatedAtUtc < otpOptions.ResendCooldown)
        {
            // Silently skipping the send keeps the answer uniform; the player already has a code.
            return sent;
        }

        var sinceHourAgo = now - TimeSpan.FromHours(1);
        var sendsLastHour = await db.PlayerPhoneOtps.CountAsync(
            otp => otp.PlayerAccountId == account.PlayerAccountId
                && otp.Purpose == PlayerPhoneOtpPurpose.SignIn
                && otp.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= otpOptions.MaxSendsPerHour)
        {
            return sent;
        }

        var code = generator.Generate();
        var otpId = Guid.NewGuid();
        db.PlayerPhoneOtps.Add(new PlayerPhoneOtpEntity
        {
            PlayerPhoneOtpId = otpId,
            PlayerAccountId = account.PlayerAccountId,
            OrganizationId = organizationId,
            Phone = normalizedPhone,
            Purpose = PlayerPhoneOtpPurpose.SignIn,
            CodeHash = hasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + otpOptions.Lifetime,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync(cancellationToken);

        await notifications.SendNowAsync(
            new NotificationRequest(
                TemplateKey: NotificationTemplateKeys.PlayerSignInCode,
                Category: NotificationCategory.Transactional,
                Recipient: new NotificationRecipient(
                    Locale: account.PreferredLocale ?? notificationOptions.DefaultLocale,
                    PhoneNumber: "+" + normalizedPhone,
                    PlayerAccountId: account.PlayerAccountId),
                Tokens: new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["expiresInMinutes"] = ((int)otpOptions.Lifetime.TotalMinutes).ToString(),
                    ["displayName"] = account.DisplayName,
                },
                IdempotencyKey: $"player-sign-in-code:{otpId:N}",
                PreferredChannels: [NotificationChannel.Sms],
                OrganizationId: organizationId),
            cancellationToken);

        // An SMS that failed to leave is reported as sent as well: the alternative tells a caller
        // holding an unknown number nothing, and a caller holding a real one that it is real.
        return sent;
    }

    public async Task<PlayerCodeSignInResult> ConfirmAsync(
        Guid organizationId, string rawPhone, string code, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new PlayerCodeSignInResult(PlayerCodeSignInStatus.NoActiveCode, null, 0);
        }

        var now = timeProvider.GetUtcNow();

        var account = await db.PlayerAccounts.FirstOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId
                && candidate.IsActive
                && candidate.PhoneNumber == "+" + normalizedPhone,
            cancellationToken);
        if (account is null)
        {
            return new PlayerCodeSignInResult(PlayerCodeSignInStatus.NoActiveCode, null, 0);
        }

        var otp = await db.PlayerPhoneOtps
            .Where(candidate => candidate.PlayerAccountId == account.PlayerAccountId
                && candidate.Purpose == PlayerPhoneOtpPurpose.SignIn
                && candidate.ConsumedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return new PlayerCodeSignInResult(PlayerCodeSignInStatus.NoActiveCode, null, 0);
        }

        if (otp.ExpiresAtUtc <= now)
        {
            return new PlayerCodeSignInResult(PlayerCodeSignInStatus.Expired, null, 0);
        }

        if (otp.AttemptCount >= otpOptions.MaxAttempts)
        {
            return new PlayerCodeSignInResult(PlayerCodeSignInStatus.TooManyAttempts, null, 0);
        }

        if (hasher.Hash(PhoneOtpCode.KeepDigits(code)) != otp.CodeHash)
        {
            otp.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, otpOptions.MaxAttempts - otp.AttemptCount);
            return new PlayerCodeSignInResult(PlayerCodeSignInStatus.InvalidCode, null, remaining);
        }

        var credential = await db.PlayerCredentials.FirstOrDefaultAsync(
            candidate => candidate.PlayerAccountId == account.PlayerAccountId, cancellationToken);
        if (credential is null)
        {
            credential = new PlayerCredentialEntity
            {
                PlayerCredentialId = Guid.NewGuid(),
                PlayerAccountId = account.PlayerAccountId,
                OrganizationId = account.OrganizationId,
                CreatedAtUtc = now,
            };
            db.PlayerCredentials.Add(credential);
        }

        // Reading the code off this phone is the same proof the verification flow asks for, so a
        // player who signs in this way has confirmed the number by definition.
        credential.PhoneVerified = true;
        credential.PhoneVerifiedAtUtc ??= now;
        credential.FailedLoginCount = 0;
        credential.LockedUntilUtc = null;
        credential.UpdatedAtUtc = now;
        otp.ConsumedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        // Токен выдаётся человеку, а названный при входе клуб закрепляется за токеном: клиент,
        // который про выбор клуба ничего не знает, попадает туда же, куда и вчера. Счёт, ещё не
        // подшитый к личности (дубль внутри клуба после переноса), входит по-старому — иначе
        // человек остался бы без входа вовсе.
        var person = account.PlatformPersonId is { } platformPersonId
            ? await db.PlatformPersons.FirstOrDefaultAsync(
                candidate => candidate.PlatformPersonId == platformPersonId && candidate.IsActive,
                cancellationToken)
            : null;

        if (person is not null)
        {
            // Прочитать код с этого телефона — то же доказательство владения номером, что и в
            // проверке номера, поэтому личность считается подтверждённой.
            person.PhoneVerifiedAtUtc ??= now;
            person.UpdatedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);

            var session = await personTokenService.IssueAsync(person, account, cancellationToken);

            // Этот маршрут отвечает ровно тем же телом, что и вчера: клуб здесь назван входом, и
            // новых полей старому клиенту тут не нужно. Расширенную сессию отдаёт регистрация.
            var personTokens = new PlayerSignInResponse(
                account.PlayerAccountId,
                account.OrganizationId,
                session.DisplayName,
                session.PhoneVerified,
                session.AccessToken,
                session.AccessTokenExpiresAtUtc,
                session.RefreshToken,
                session.RefreshTokenExpiresAtUtc);
            return new PlayerCodeSignInResult(PlayerCodeSignInStatus.SignedIn, personTokens, otpOptions.MaxAttempts);
        }

        var tokens = await tokenService.IssueAsync(account, phoneVerified: true, cancellationToken);
        return new PlayerCodeSignInResult(PlayerCodeSignInStatus.SignedIn, tokens, otpOptions.MaxAttempts);
    }
}
