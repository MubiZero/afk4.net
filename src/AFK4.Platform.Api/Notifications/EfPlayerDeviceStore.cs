using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

public sealed class EfPlayerDeviceStore(PlatformDbContext dbContext, TimeProvider timeProvider) : IPlayerDeviceStore
{
    public async Task RegisterAsync(
        Guid playerAccountId,
        string pushToken,
        string platform,
        string? locale,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.PlayerDevices
            .FirstOrDefaultAsync(device => device.PushToken == pushToken, cancellationToken);

        if (existing is null)
        {
            dbContext.PlayerDevices.Add(new PlayerDeviceEntity
            {
                PlayerDeviceId = Guid.NewGuid(),
                PlayerAccountId = playerAccountId,
                PushToken = pushToken,
                Platform = platform,
                Locale = locale,
                CreatedUtc = now,
                LastSeenUtc = now,
            });
        }
        else
        {
            // Тот же телефон под другим аккаунтом: у строки меняется владелец. Иначе пуши для
            // нового игрока ушли бы предыдущему — на том же экране.
            existing.PlayerAccountId = playerAccountId;
            existing.Platform = platform;
            existing.Locale = locale;
            existing.LastSeenUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid playerAccountId, string pushToken, CancellationToken cancellationToken)
    {
        var device = await dbContext.PlayerDevices.FirstOrDefaultAsync(
            candidate => candidate.PushToken == pushToken && candidate.PlayerAccountId == playerAccountId,
            cancellationToken);
        if (device is null)
        {
            return;
        }

        dbContext.PlayerDevices.Remove(device);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerDeviceEntity>> ListAsync(Guid playerAccountId, CancellationToken cancellationToken) =>
        await dbContext.PlayerDevices
            .AsNoTracking()
            .Where(device => device.PlayerAccountId == playerAccountId)
            .OrderBy(device => device.CreatedUtc)
            .ToListAsync(cancellationToken);

    public async Task ForgetAsync(string pushToken, CancellationToken cancellationToken)
    {
        var device = await dbContext.PlayerDevices
            .FirstOrDefaultAsync(candidate => candidate.PushToken == pushToken, cancellationToken);
        if (device is null)
        {
            return;
        }

        dbContext.PlayerDevices.Remove(device);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
