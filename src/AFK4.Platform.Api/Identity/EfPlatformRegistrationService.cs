using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Самостоятельная регистрация. Сегодня карточку заводит администратор клуба, и человек, скачавший
/// приложение дома, упирается в стену: войти он может только туда, куда его уже внесли.
///
/// Главное свойство этой пары маршрутов — не «работает», а «молчит». Просьба прислать код отвечает
/// одинаково знакомому и незнакомому номеру: одним и тем же телом, одним и тем же кодом ответа и —
/// поскольку SMS уходит в обоих случаях — за одно и то же время. Иначе приложение становится
/// справочником «кто играет в этой сети», и проверить это можно с любого телефона.
/// </summary>
public sealed class EfPlatformRegistrationService(
    PlatformDbContext db,
    PhoneKeyedOtpStore otpStore,
    INotificationService notifications,
    IPlatformPersonTokenService personTokenService,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> notificationOptions) : IPlatformRegistrationService
{
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;

    public async Task<PhoneVerificationStartResult> StartAsync(
        string rawPhone, CancellationToken cancellationToken)
    {
        var sent = new PhoneVerificationStartResult(
            PhoneVerificationStartStatus.Sent,
            otpStore.ExpiresInSeconds,
            otpStore.ResendAfterSeconds,
            null);

        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            // Номер, который не может принадлежать никому, ничего о людях не выдаёт.
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.InvalidPhone, 0, 0, null);
        }

        var issued = await otpStore.IssueAsync(
            normalizedPhone, PlatformPhoneOtpPurpose.Registration, cancellationToken);
        if (issued is not { Code: { } code, OtpId: { } otpId })
        {
            // Кулдаун и исчерпанный часовой лимит выглядят снаружи ровно как отправленный код:
            // иначе по ним считают, сколько SMS ушло на чужой номер.
            return sent;
        }

        // Язык берём общий, а не язык личности: чтобы узнать её язык, личность надо сначала найти,
        // а искать её здесь нельзя. В SMS всё равно едет один код и ничего больше.
        await notifications.SendNowAsync(
            new NotificationRequest(
                TemplateKey: NotificationTemplateKeys.PlayerSignInCode,
                Category: NotificationCategory.Transactional,
                Recipient: new NotificationRecipient(
                    Locale: notificationOptions.DefaultLocale,
                    PhoneNumber: "+" + normalizedPhone),
                Tokens: new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["expiresInMinutes"] = otpStore.ExpiresInMinutes.ToString(),
                },
                IdempotencyKey: $"platform-registration-code:{otpId:N}",
                PreferredChannels: [NotificationChannel.Sms]),
            cancellationToken);

        // Не уехавшая SMS тоже объявляется отправленной: иначе звонящий с настоящим номером
        // получает один ответ, а с выдуманным — другой.
        return sent;
    }

    public async Task<PlatformRegistrationConfirmResult> ConfirmAsync(
        string rawPhone, string code, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new PlatformRegistrationConfirmResult(
                PlatformRegistrationConfirmStatus.NoActiveCode, null, 0);
        }

        var checkResult = await otpStore.ConsumeAsync(
            normalizedPhone, PlatformPhoneOtpPurpose.Registration, code, cancellationToken);
        if (checkResult.Status != PhoneOtpCheckStatus.Confirmed)
        {
            return new PlatformRegistrationConfirmResult(
                checkResult.Status switch
                {
                    PhoneOtpCheckStatus.InvalidCode => PlatformRegistrationConfirmStatus.InvalidCode,
                    PhoneOtpCheckStatus.Expired => PlatformRegistrationConfirmStatus.Expired,
                    PhoneOtpCheckStatus.TooManyAttempts => PlatformRegistrationConfirmStatus.TooManyAttempts,
                    _ => PlatformRegistrationConfirmStatus.NoActiveCode,
                },
                null,
                checkResult.RemainingAttempts);
        }

        var phoneNumber = "+" + normalizedPhone;
        var now = timeProvider.GetUtcNow();

        var person = await db.PlatformPersons.FirstOrDefaultAsync(
            candidate => candidate.PhoneNumber == phoneNumber, cancellationToken);

        if (person is { IsActive: false })
        {
            return new PlatformRegistrationConfirmResult(
                PlatformRegistrationConfirmStatus.PersonDeactivated, null, 0);
        }

        if (person is null)
        {
            // Имя и язык здесь не спрашиваются: их спрашивает следующий экран, уже под токеном.
            // PIN не спрашивается тем более — он задаётся в ту секунду, когда впервые нужен.
            person = new PlatformPersonEntity
            {
                PlatformPersonId = Guid.NewGuid(),
                PhoneNumber = phoneNumber,
                DisplayName = string.Empty,
                PhoneVerifiedAtUtc = now,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.PlatformPersons.Add(person);
        }
        else
        {
            // Прочитать код с этого телефона — то же доказательство владения номером, что и в
            // проверке номера.
            person.PhoneVerifiedAtUtc ??= now;
            person.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Клуб закрепляется за токеном, только когда он один и выбирать не из чего. При двух и
        // более выбор делает человек заголовком запроса: подставить клуб за него значит показать
        // ему чужой кошелёк там, где он ждал свой.
        var accounts = await db.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.PlatformPersonId == person.PlatformPersonId && account.IsActive)
            .Take(2)
            .ToListAsync(cancellationToken);
        var pinnedAccount = accounts.Count == 1 ? accounts[0] : null;

        var session = await personTokenService.IssueAsync(person, pinnedAccount, cancellationToken);
        return new PlatformRegistrationConfirmResult(
            PlatformRegistrationConfirmStatus.SignedIn, session, checkResult.RemainingAttempts);
    }
}
