using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Games;

/// <summary>
/// Где кеш игр филиала (docs/operations/game-cache-lancache.md): свой сервер LANCache клуба, если задан,
/// иначе встроенный кеш агента на ПК администратора — без всякой настройки со стороны клуба.
/// </summary>
public static class BranchGameCache
{
    /// <summary>ПК администратора, чей встроенный кеш поднят и на связи. Из нескольких — самый давний.</summary>
    public static Task<DeviceEntity?> HostAsync(PlatformDbContext dbContext, Guid branchId, CancellationToken cancellationToken) =>
        dbContext.Devices.AsNoTracking()
            .Where(device => device.BranchId == branchId
                && device.Role == DeviceRoleNames.ManagerWorkstation
                && device.IsOnline
                && device.GameCacheServing == true
                && device.NetworkAddress != null)
            .OrderBy(device => device.EnrolledAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}
