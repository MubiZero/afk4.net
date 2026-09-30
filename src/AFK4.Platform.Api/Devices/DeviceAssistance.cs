using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>Вызов администратора с ПК (<see cref="DeviceEntity.AssistanceRequestedAtUtc"/>).</summary>
public static class DeviceAssistance
{
    /// <summary>
    /// Вызов принадлежит человеку за ПК: кончилась его сессия — кончился и вызов. Иначе на свободном
    /// ПК висело «зовёт 2 ч 42 м», а потом вызов доставался следующему гостю. Сохраняет вызывающий —
    /// тем же SaveChanges, что закрывает сессию.
    /// </summary>
    public static async Task ClearOnSessionEndAsync(
        PlatformDbContext dbContext,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices
            .SingleOrDefaultAsync(candidate => candidate.DeviceId == deviceId, cancellationToken);
        if (device is not null)
        {
            device.AssistanceRequestedAtUtc = null;
        }
    }
}
