using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Токены игрока, выданные на игровом ПК, гасит сервер, а не хост.
///
/// Хост тоже выходит из аккаунта, когда игрок ушёл, но упавший или подменённый хост этого не
/// сделает — и следующий за машиной играл бы на чужие деньги. Поэтому граница живёт здесь: как
/// только за ПК больше некому сидеть, его токены гаснут.
/// </summary>
public interface IDeviceBoundPlayerTokens
{
    /// <summary>
    /// Погасить все живые токены ПК сейчас: новый вход на этой машине, перепривязка места, отказ
    /// от устройства или отзыв его ключа. Сохраняет вызывающий.
    /// </summary>
    Task RevokeForDeviceAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Погасить токены ПК, у которого нет живой сессии, если за ним больше некому сидеть: после
    /// входа закончилась сессия и итог досмотрен, или вход был давно, а сессия так и не началась.
    /// Зовётся на сердцебиении свободного ПК — занятый сюда не приходит. Сохраняет сам и
    /// возвращает, сколько токенов погашено.
    /// </summary>
    Task<int> ExpireIdleAsync(Guid deviceId, CancellationToken cancellationToken);
}

public sealed class EfDeviceBoundPlayerTokens(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IDeviceBoundPlayerTokens
{
    /// <summary>
    /// Сколько после конца сессии живёт вход: столько экран итога ждёт оценки визита и чаевых.
    /// Итог сам уходит через 25 с — запас на медленное сердцебиение.
    /// </summary>
    public static readonly TimeSpan SummaryWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Сколько живёт вход без начатой сессии. Выбрать время и нажать «Начать» — меньше минуты;
    /// оболочка сама выходит через минуту тишины, это — граница на случай, если она не вышла.
    /// </summary>
    public static readonly TimeSpan PreSessionWindow = TimeSpan.FromMinutes(5);

    public async Task RevokeForDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await RevokeWhereAsync(deviceId, now, _ => true, cancellationToken);
    }

    public async Task<int> ExpireIdleAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Сердцебиение стучит с каждого ПК раз в несколько секунд, а живые токены на машине
        // бывают редко: сначала дешёвый вопрос «есть ли что гасить» по индексу.
        var anyLive = await dbContext.PlatformPersonRefreshTokens
            .AsNoTracking()
            .AnyAsync(token => token.DeviceId == deviceId && token.RevokedAtUtc == null && token.ExpiresAtUtc > now, cancellationToken)
            || await dbContext.PlatformPersonAccessTokens
                .AsNoTracking()
                .AnyAsync(token => token.DeviceId == deviceId && token.RevokedAtUtc == null && token.ExpiresAtUtc > now, cancellationToken);
        if (!anyLive)
        {
            return 0;
        }

        // Только читаем: сессии — чужие данные, здесь важно лишь, когда на машине кончилась
        // последняя.
        var lastSessionEndedAtUtc = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.DeviceId == deviceId && session.EndedAtUtc != null)
            .MaxAsync(session => session.EndedAtUtc, cancellationToken);

        // Итог ещё на экране — не трогаем никого: оценка визита и чаевые идут от имени вошедшего.
        var summaryShowing = lastSessionEndedAtUtc is { } endedAt && now - endedAt <= SummaryWindow;
        if (summaryShowing)
        {
            return 0;
        }

        var revoked = await RevokeWhereAsync(
            deviceId,
            now,
            signedInAtUtc =>
                // После входа на этой машине сессия закончилась, итог досмотрен.
                (lastSessionEndedAtUtc is { } ended && ended >= signedInAtUtc)
                // Вошёл давно, а сессия так и не началась.
                || now - signedInAtUtc > PreSessionWindow,
            cancellationToken);
        if (revoked > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return revoked;
    }

    private async Task<int> RevokeWhereAsync(
        Guid deviceId,
        DateTimeOffset now,
        Func<DateTimeOffset, bool> shouldRevoke,
        CancellationToken cancellationToken)
    {
        var refreshTokens = await dbContext.PlatformPersonRefreshTokens
            .Where(token => token.DeviceId == deviceId && token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);
        var accessTokens = await dbContext.PlatformPersonAccessTokens
            .Where(token => token.DeviceId == deviceId && token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        var revoked = 0;
        foreach (var token in refreshTokens.Where(token => shouldRevoke(token.DeviceSignedInAtUtc ?? token.CreatedAtUtc)))
        {
            token.RevokedAtUtc = now;
            revoked++;
        }

        foreach (var token in accessTokens.Where(token => shouldRevoke(token.DeviceSignedInAtUtc ?? token.CreatedAtUtc)))
        {
            token.RevokedAtUtc = now;
            revoked++;
        }

        return revoked;
    }
}
