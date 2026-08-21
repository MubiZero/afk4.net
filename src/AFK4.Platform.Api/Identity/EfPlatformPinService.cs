using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Сетевой PIN целиком: как его задают и как его проверяют.
///
/// Хранится он ровно так же, как раньше хранился клубный, — хешем `PasswordHasher`, — но лежит на
/// личности, и это меняет всё остальное: задать его может только сам человек, а сработает он в
/// любом клубе сети. Клубный <see cref="PlayerCredentialEntity.PasswordHash"/> отсюда не читается
/// никогда: PIN, назначенный администратором, — это чужой ключ от чужих клубов.
/// </summary>
public sealed class EfPlatformPinService(
    PlatformDbContext dbContext,
    IPlayerClubMembershipService clubMemberships,
    IPlatformPersonTokenService tokenService,
    TimeProvider timeProvider,
    ILogger<EfPlatformPinService> logger) : IPlatformPinService
{
    private const int MaxFailedAttempts = 5;

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly PasswordHasher<PlatformPersonEntity> passwordHasher = new();

    public async Task<SetPinStatus> SetAsync(
        Guid platformPersonId, string? pin, CancellationToken cancellationToken)
    {
        if (!PinFormat.IsWellFormed(pin))
        {
            return SetPinStatus.InvalidPin;
        }

        var person = await dbContext.PlatformPersons.SingleOrDefaultAsync(
            candidate => candidate.PlatformPersonId == platformPersonId, cancellationToken);
        if (person is null)
        {
            return SetPinStatus.PersonNotFound;
        }

        var now = timeProvider.GetUtcNow();
        person.PinHash = passwordHasher.HashPassword(person, pin!);
        person.PinSetAtUtc = now;

        // Блокировка снимается вместе с новым PIN. Пять чужих неверных попыток у ПК запирают
        // самопосадку на четверть часа, и человек, пришедший в приложение именно за этим, обязан
        // выйти из неё сам — иначе наказан тот, кого подбирали, а не тот, кто подбирал.
        person.PinFailedCount = 0;
        person.PinLockedUntilUtc = null;
        person.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return SetPinStatus.Updated;
    }

    public async Task<PinSignInResult> SignInAsync(
        Guid organizationId,
        string? rawPhone,
        string? pin,
        Guid? branchId,
        CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null || string.IsNullOrEmpty(pin))
        {
            return PinSignInResult.Refused;
        }

        var phoneNumber = "+" + normalizedPhone;
        var person = await dbContext.PlatformPersons.SingleOrDefaultAsync(
            candidate => candidate.PhoneNumber == phoneNumber, cancellationToken);

        // Ни одна из этих причин наружу не выходит: «нет такого номера» и «PIN не задан» —
        // это ответы на вопрос, кто в этой сети играет.
        if (person is null
            || !person.IsActive
            || person.NetworkBanAtUtc is not null
            || person.PinHash is null)
        {
            return PinSignInResult.Refused;
        }

        var now = timeProvider.GetUtcNow();
        if (person.PinLockedUntilUtc is { } lockedUntil && lockedUntil > now)
        {
            return PinSignInResult.Refused;
        }

        var verification = passwordHasher.VerifyHashedPassword(person, person.PinHash, pin);
        if (verification == PasswordVerificationResult.Failed)
        {
            person.PinFailedCount++;
            if (person.PinFailedCount >= MaxFailedAttempts)
            {
                person.PinLockedUntilUtc = now.Add(LockoutDuration);
            }

            person.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return PinSignInResult.Refused;
        }

        person.PinFailedCount = 0;
        person.PinLockedUntilUtc = null;
        person.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        // Человек стоит у ПК этого клуба — дальше него это уже не «полистать витрину», а приход.
        // Счёт открывается здесь же, если его не было: тот же путь, что у первого действия (Ф3).
        var membership = await clubMemberships.EnsureAsync(
            person.PlatformPersonId, organizationId, branchId, cancellationToken);
        if (membership.Account is null)
        {
            // Верный PIN, но клуба назвать не смогли: несколько филиалов и ни одного счёта, либо
            // клуба нет вовсе. Отказ тот же самый — сказать «PIN верный, но…» значит подтвердить
            // подбирающему, что PIN он угадал.
            logger.LogWarning(
                "PIN accepted but the club account could not be opened for organization {OrganizationId}: {Error}.",
                organizationId,
                membership.Error);
            return PinSignInResult.Refused;
        }

        return PinSignInResult.SignedIn(
            await tokenService.IssueAsync(person, membership.Account, cancellationToken));
    }
}
