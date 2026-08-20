using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

public sealed class EfPlayerReputationService(PlatformDbContext db, TimeProvider timeProvider)
    : IPlayerReputationService
{
    /// <summary>Заявка, которая ещё чего-то ждёт от клуба. Снятая основанием не остаётся.</summary>
    private static readonly string[] LiveReservationStates =
    [
        ReservationStateNames.Pending,
        ReservationStateNames.Confirmed,
        ReservationStateNames.Seated
    ];

    public async Task<PlayerReputationDto?> GetForLinkedPersonAsync(
        Guid organizationId, Guid platformPersonId, CancellationToken cancellationToken)
    {
        var person = await db.PlatformPersons
            .AsNoTracking()
            .Where(candidate => candidate.PlatformPersonId == platformPersonId)
            .Select(candidate => new { candidate.PhoneNumber, Banned = candidate.NetworkBanAtUtc != null })
            .FirstOrDefaultAsync(cancellationToken);

        // Несуществующая личность и личность чужого клуба уходят одной дорогой: наружу оба случая
        // обязаны выглядеть одинаково, иначе перебор идентификаторов становится справочником.
        if (person is null ||
            !await HasBasisAsync(organizationId, platformPersonId, person.PhoneNumber, cancellationToken))
        {
            return null;
        }

        return await BuildAsync(platformPersonId, person.Banned, cancellationToken);
    }

    public async Task<PlayerReputationDto?> GetByExactPhoneAsync(
        string rawPhone, CancellationToken cancellationToken)
    {
        var normalized = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalized is null)
        {
            return null;
        }

        var phoneNumber = "+" + normalized;
        var person = await db.PlatformPersons
            .AsNoTracking()
            .Where(candidate => candidate.PhoneNumber == phoneNumber)
            .Select(candidate => new
            {
                candidate.PlatformPersonId,
                Banned = candidate.NetworkBanAtUtc != null
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Номера, которого сеть не знает, здесь нет — и это ровно то же, что человек без визитов.
        return person is null
            ? new PlayerReputationDto(0, 0, false, await ReadAsOfAsync(cancellationToken))
            : await BuildAsync(person.PlatformPersonId, person.Banned, cancellationToken);
    }

    /// <summary>
    /// Связь с клубом либо живая заявка в него. Заявку ищем и по счёту, и по самому номеру:
    /// гостя, который позвонил на стойку, записывают одним телефоном, счёта у него ещё нет.
    /// </summary>
    private async Task<bool> HasBasisAsync(
        Guid organizationId, Guid platformPersonId, string phoneNumber, CancellationToken cancellationToken)
    {
        var linked = await db.PlayerAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.OrganizationId == organizationId
                    && account.PlatformPersonId == platformPersonId,
                cancellationToken);
        if (linked)
        {
            return true;
        }

        // Номера в заявках лежат так, как их набрал администратор, поэтому сравниваем
        // нормализованные формы. Выборка ограничена живыми заявками одного клуба — это десятки
        // строк, а не журнал за год.
        var normalizedPhone = PhoneNumberNormalizer.Normalize(phoneNumber);
        if (normalizedPhone is null)
        {
            return false;
        }

        var livePhones = await db.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.OrganizationId == organizationId
                && reservation.PhoneNumber != null
                && LiveReservationStates.Contains(reservation.State))
            .Select(reservation => reservation.PhoneNumber!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return livePhones.Any(candidate =>
            string.Equals(PhoneNumberNormalizer.Normalize(candidate), normalizedPhone, StringComparison.Ordinal));
    }

    private async Task<PlayerReputationDto> BuildAsync(
        Guid platformPersonId, bool banned, CancellationToken cancellationToken)
    {
        var snapshot = await db.PlatformReputationSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.PlatformPersonId == platformPersonId, cancellationToken);

        return new PlayerReputationDto(
            snapshot?.NetworkVisits ?? 0,
            snapshot?.NetworkNoShows ?? 0,
            banned,
            await ReadAsOfAsync(cancellationToken));
    }

    /// <summary>
    /// «На когда посчитано» — величина сети, а не человека: время из его собственной строки
    /// выдавало бы, есть ли у него строка вообще, то есть отличало бы новичка от незнакомца.
    /// Пока в сети не пересчитано ничего, отвечаем началом текущих суток — значение общее для
    /// всех и потому такое же немое.
    /// </summary>
    private async Task<DateTimeOffset> ReadAsOfAsync(CancellationToken cancellationToken)
    {
        var latest = await db.PlatformReputationSnapshots
            .AsNoTracking()
            .MaxAsync(row => (DateTimeOffset?)row.CalculatedAtUtc, cancellationToken);

        var now = timeProvider.GetUtcNow();
        return latest ?? new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
    }
}
