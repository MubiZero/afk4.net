using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Analytics;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Насколько далеко зашёл переход на сетевой PIN. Показатель существует ровно затем, чтобы у
/// перехода был конец: без числа «уже задали» вечный режим совместимости не отличить от готового
/// продукта, а именно вечного режима мы и не хотим.
///
/// Знаменателей два, и оба честные. Человек, чья карточка не подшита к личности, PIN задать не
/// может вовсе — это гость, которого завёл оператор, и он по замыслу садится не сам. Смешать его с
/// теми, кто PIN задать может, значит сделать порог недостижимым и никогда не закрыть переход.
/// </summary>
public sealed class PinAdoptionReader(PlatformDbContext dbContext, TimeProvider timeProvider)
{
    public const int DefaultWindowDays = 30;

    private const int MinWindowDays = 7;

    private const int MaxWindowDays = 90;

    public async Task<PinAdoptionDto> ReadAsync(int windowDays, CancellationToken cancellationToken)
    {
        var window = Math.Clamp(windowDays, MinWindowDays, MaxWindowDays);
        var now = timeProvider.GetUtcNow();
        var since = now.AddDays(-window);

        var activeAccountIds = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.PlayerAccountId != null
                && (session.StartedAtUtc ?? session.RequestedAtUtc) >= since)
            .Select(session => session.PlayerAccountId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activePersonIds = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(account => activeAccountIds.Contains(account.PlayerAccountId)
                && account.PlatformPersonId != null)
            .Select(account => account.PlatformPersonId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var withPin = await dbContext.PlatformPersons
            .AsNoTracking()
            .CountAsync(
                person => activePersonIds.Contains(person.PlatformPersonId) && person.PinSetAtUtc != null,
                cancellationToken);

        var adoptionPercent = activePersonIds.Count == 0
            ? 0
            : (int)Math.Round(100.0 * withPin / activePersonIds.Count, MidpointRounding.AwayFromZero);

        return new PinAdoptionDto(
            now,
            window,
            activeAccountIds.Count,
            activePersonIds.Count,
            withPin,
            adoptionPercent);
    }
}
