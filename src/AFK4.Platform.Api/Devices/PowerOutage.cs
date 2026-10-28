using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications.Telegram;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>
/// «Свет пропал — сессия не пропала» (docs/roadmap/power-outage-resume.md). Сервер по тишине ПК не отличит
/// отключение света от пропавшего интернета — а без интернета гости играют дальше, и паузить их нельзя.
/// Отличает сам ПК: включившись, агент говорит «был обесточен с такого-то момента». Отключение света в
/// клубе — когда так говорят хотя бы два ПК с одним началом или один, пока другой ПК работал от ИБП:
/// один выдернутый из розетки ПК времени не вернёт. Тогда каждому такому ПК сессия продлевается на простой.
/// </summary>
public sealed class PowerOutage(
    PlatformDbContext dbContext,
    ISessionCommandService sessions,
    TimeProvider clock,
    ILogger<PowerOutage> logger,
    TelegramReports? telegramReports = null)
{
    /// <summary>ПК одного отключения гаснут почти разом, а их «последний раз был жив» расходится на секунды.</summary>
    public static readonly TimeSpan SameOutageWindow = TimeSpan.FromMinutes(3);

    /// <summary>Дольше — уже не отключение, которое стоит возвращать гостю (и, скорее, сбитые часы).</summary>
    private static readonly TimeSpan MaxOutage = TimeSpan.FromDays(1);

    /// <summary>Отметка ИБП и сообщение об обесточивании из сердцебиения. Устройство уже отслеживается контекстом.</summary>
    public async Task RecordAsync(DeviceEntity device, DeviceHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (request.OnBatteryPower is { } onBattery)
        {
            if (onBattery && device.OnBatterySinceUtc is null)
            {
                device.OnBatterySinceUtc = now;
            }
            else if (!onBattery)
            {
                device.OnBatterySinceUtc = null;
            }
        }

        if (request.PowerLostFromUtc is not { } fromUtc || request.UptimeSeconds is not { } uptime || uptime < 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var toUtc = now - TimeSpan.FromSeconds(uptime);
        if (toUtc <= fromUtc || toUtc - fromUtc > MaxOutage
            || await dbContext.DevicePowerLosses.AnyAsync(loss => loss.DeviceId == device.DeviceId && loss.FromUtc == fromUtc, cancellationToken))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        dbContext.DevicePowerLosses.Add(new DevicePowerLossEntity
        {
            DevicePowerLossId = Guid.NewGuid(),
            OrganizationId = device.OrganizationId,
            BranchId = device.BranchId,
            DeviceId = device.DeviceId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            ReportedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await RestoreIfClubOutageAsync(device.OrganizationId, device.BranchId, fromUtc, cancellationToken);
    }

    private async Task RestoreIfClubOutageAsync(Guid organizationId, Guid branchId, DateTimeOffset fromUtc, CancellationToken cancellationToken)
    {
        var earliest = fromUtc - SameOutageWindow;
        var latest = fromUtc + SameOutageWindow;
        var cluster = await dbContext.DevicePowerLosses
            .Where(loss => loss.BranchId == branchId && loss.FromUtc >= earliest && loss.FromUtc <= latest)
            .ToListAsync(cancellationToken);
        var powerLostPcs = cluster.Select(loss => loss.DeviceId).Distinct().Count();
        var upsWitness = await dbContext.Devices.AnyAsync(
            device => device.BranchId == branchId && device.OnBatterySinceUtc != null
                && device.OnBatterySinceUtc >= earliest && device.OnBatterySinceUtc <= latest,
            cancellationToken);
        if (powerLostPcs < 2 && !(powerLostPcs == 1 && upsWitness))
        {
            return;
        }

        var firstConfirmation = cluster.All(loss => !loss.Compensated);
        foreach (var loss in cluster.Where(loss => !loss.Compensated))
        {
            // Забираем запись раньше, чем возвращаем время: два ПК, включившихся разом, иначе вернули бы его дважды.
            loss.Compensated = true;
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.Entry(loss).State = EntityState.Detached;
                continue;
            }

            var restored = await sessions.RestoreAfterPowerLossAsync(loss.DeviceId, loss.FromUtc, loss.ToUtc, cancellationToken);
            if (restored is not null)
            {
                loss.SessionId = restored.SessionId;
                loss.ReturnedMinutes = restored.ReturnedMinutes;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Power outage at branch {BranchId}: session {SessionId} got {Minutes} min back{Paused}.",
                    branchId, restored.SessionId, restored.ReturnedMinutes, restored.Paused ? " and waits paused" : string.Empty);
            }
        }

        if (firstConfirmation && telegramReports is not null)
        {
            await telegramReports.PowerOutageAsync(organizationId, branchId, cluster.Min(loss => loss.FromUtc), powerLostPcs, cancellationToken);
        }
    }
}
