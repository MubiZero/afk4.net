using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

public sealed class EfPlayerDeviceStore(PlatformDbContext dbContext, TimeProvider timeProvider) : IPlayerDeviceStore
{
    /// <summary>
    /// Телефон — адрес доставки для каждого клубного счёта человека, а не для одного: у кого счета
    /// в двух клубах, тому пуши нужны от обоих. Раньше вход во второй клуб переносил строку туда, и
    /// первый клуб замолкал. Другой человек на том же телефоне вытесняет прежнего целиком: его
    /// уведомления на чужом экране не нужны.
    /// </summary>
    public async Task RegisterAsync(
        Guid playerAccountId,
        string pushToken,
        string platform,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var person = await PersonOfAsync(playerAccountId, cancellationToken);
        var rows = await RowsOfTokenAsync(pushToken, cancellationToken);

        foreach (var row in rows.Where(row => row.Device.PlayerAccountId != playerAccountId && !SamePerson(person, row.Person)))
        {
            dbContext.PlayerDevices.Remove(row.Device);
        }

        var own = rows.FirstOrDefault(row => row.Device.PlayerAccountId == playerAccountId)?.Device;
        if (own is null)
        {
            dbContext.PlayerDevices.Add(new PlayerDeviceEntity
            {
                PlayerDeviceId = Guid.NewGuid(),
                PlayerAccountId = playerAccountId,
                PushToken = pushToken,
                Platform = platform,
                CreatedUtc = now,
                LastSeenUtc = now,
            });
        }
        else
        {
            own.Platform = platform;
            own.LastSeenUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Выход из приложения: телефон перестаёт получать пуши всех клубов этого человека — выходят из
    /// приложения целиком, а не из одного клуба. Чужие строки не трогаются.
    /// </summary>
    public async Task RemoveAsync(Guid playerAccountId, string pushToken, CancellationToken cancellationToken)
    {
        var person = await PersonOfAsync(playerAccountId, cancellationToken);
        var rows = await RowsOfTokenAsync(pushToken, cancellationToken);
        var mine = rows.Where(row => row.Device.PlayerAccountId == playerAccountId || SamePerson(person, row.Person)).ToList();
        if (mine.Count == 0)
        {
            return;
        }

        dbContext.PlayerDevices.RemoveRange(mine.Select(row => row.Device));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerDeviceEntity>> ListAsync(Guid playerAccountId, CancellationToken cancellationToken) =>
        await dbContext.PlayerDevices
            .AsNoTracking()
            .Where(device => device.PlayerAccountId == playerAccountId)
            .OrderBy(device => device.CreatedUtc)
            .ToListAsync(cancellationToken);

    /// <summary>Токен отозван сервисом доставки: он мёртв для всех счетов, на которые записан.</summary>
    public async Task ForgetAsync(string pushToken, CancellationToken cancellationToken)
    {
        var devices = await dbContext.PlayerDevices
            .Where(candidate => candidate.PushToken == pushToken)
            .ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return;
        }

        dbContext.PlayerDevices.RemoveRange(devices);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record TokenRow(PlayerDeviceEntity Device, Guid? Person);

    private Task<Guid?> PersonOfAsync(Guid playerAccountId, CancellationToken cancellationToken) =>
        dbContext.PlayerAccounts.AsNoTracking()
            .Where(account => account.PlayerAccountId == playerAccountId)
            .Select(account => account.PlatformPersonId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<List<TokenRow>> RowsOfTokenAsync(string pushToken, CancellationToken cancellationToken)
    {
        var devices = await dbContext.PlayerDevices
            .Where(device => device.PushToken == pushToken)
            .ToListAsync(cancellationToken);
        var accountIds = devices.Select(device => device.PlayerAccountId).Distinct().ToList();
        var persons = await dbContext.PlayerAccounts.AsNoTracking()
            .Where(account => accountIds.Contains(account.PlayerAccountId))
            .Select(account => new { account.PlayerAccountId, account.PlatformPersonId })
            .ToDictionaryAsync(account => account.PlayerAccountId, account => account.PlatformPersonId, cancellationToken);
        return devices.Select(device => new TokenRow(device, persons.GetValueOrDefault(device.PlayerAccountId))).ToList();
    }

    // Человек известен и тот же. Счёт без личности (старый, до сетевого входа) — всегда «другой».
    private static bool SamePerson(Guid? a, Guid? b) => a is not null && a == b;
}
