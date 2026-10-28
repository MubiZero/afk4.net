using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>
/// Расписание ПК клуба (docs/operations/pc-health.md): в назначенное время по часам филиала — включить игровые ПК
/// по сети, чтобы к открытию они стояли загруженными; вечером — выключить свободные, чтобы ночью не жгли свет.
/// Сервер, а не агент: выключенный ПК сам себя не разбудит, а часовой пояс филиала знает сервер.
/// </summary>
public sealed class PcSchedule(
    PlatformDbContext dbContext,
    IDeviceCommandDispatchService commands,
    TimeProvider clock)
{
    /// <summary>Будим трижды за 10 минут: кто не проснулся с первого пакета, тот, может, со второго.</summary>
    public static readonly int[] WakeAttemptsAfterMinutes = [0, 5, 10];

    /// <summary>Полчаса после «выключать» гасим ПК, которые за это время освободились.</summary>
    public const int ShutdownWindowMinutes = 30;

    private static readonly string[] LiveStates = [SessionStateNames.Active, SessionStateNames.Paused, SessionStateNames.Ending];

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var schedules = await dbContext.BranchProtectionProfiles.AsNoTracking()
            .Where(profile => profile.WakeAtMinuteOfDay != null || profile.ShutdownAtMinuteOfDay != null)
            .Join(dbContext.Branches, profile => profile.BranchId, branch => branch.BranchId,
                (profile, branch) => new { profile.BranchId, profile.WakeAtMinuteOfDay, profile.ShutdownAtMinuteOfDay, branch.PreferredTimeZone })
            .ToListAsync(cancellationToken);

        var now = clock.GetUtcNow();
        var dispatched = 0;
        foreach (var schedule in schedules)
        {
            var local = TimeZoneInfo.ConvertTime(now, BranchLocalTime.ResolveZone(schedule.PreferredTimeZone));
            var minute = local.Hour * 60 + local.Minute;
            if (schedule.WakeAtMinuteOfDay is { } wakeAt && WakeAttemptsAfterMinutes.Contains(MinutesSince(wakeAt, minute)))
            {
                dispatched += await WakeAsync(schedule.BranchId, now, cancellationToken);
            }

            if (schedule.ShutdownAtMinuteOfDay is { } shutdownAt && MinutesSince(shutdownAt, minute) < ShutdownWindowMinutes)
            {
                dispatched += await ShutDownFreeAsync(schedule.BranchId, now, cancellationToken);
            }
        }

        return dispatched;
    }

    /// <summary>Сколько минут прошло с назначенной минуты, через полночь: 23:50 → 00:05 — это 15.</summary>
    public static int MinutesSince(int scheduledMinute, int currentMinute) =>
        (currentMinute - scheduledMinute + ProtectionProfileLimits.MinutesPerDay) % ProtectionProfileLimits.MinutesPerDay;

    private async Task<int> WakeAsync(Guid branchId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var silentSince = now.AddMinutes(-2);
        var sleeping = await GamingPcs(branchId)
            .Where(device => device.LastHeartbeatAtUtc == null || device.LastHeartbeatAtUtc < silentSince)
            .ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var device in sleeping)
        {
            var plan = await DeviceWake.PlanAsync(dbContext, device, clock, cancellationToken);
            if (plan.Command is not null)
            {
                await commands.EnqueueAsync(plan.HelperDeviceId, plan.Command, cancellationToken);
                sent++;
            }
        }

        return sent;
    }

    private async Task<int> ShutDownFreeAsync(Guid branchId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var aliveSince = now.AddMinutes(-1);
        var recently = now.AddMinutes(-ShutdownWindowMinutes);
        var free = await GamingPcs(branchId)
            .Where(device => device.LastHeartbeatAtUtc >= aliveSince
                && device.MaintenanceSinceUtc == null
                && !dbContext.Sessions.Any(session => session.DeviceId == device.DeviceId && LiveStates.Contains(session.State))
                // Один раз за окно: администратор, включивший ПК руками после выключения, его и получит.
                && !dbContext.DeviceCommands.Any(command => command.DeviceId == device.DeviceId
                    && command.Type == DeviceCommandTypeNames.Shutdown
                    && command.CreatedAtUtc >= recently))
            .Select(device => device.DeviceId)
            .ToListAsync(cancellationToken);
        foreach (var deviceId in free)
        {
            await commands.EnqueueAsync(deviceId, new CreateDeviceCommandRequest(
                DeviceCommandTypeNames.Shutdown, new Dictionary<string, string> { ["reason"] = "schedule" }), cancellationToken);
        }

        return free.Count;
    }

    private IQueryable<DeviceEntity> GamingPcs(Guid branchId) =>
        dbContext.Devices.Where(device => device.BranchId == branchId
            && device.Role == DeviceRoleNames.GamingPc
            && device.EnrollmentState == DeviceEnrollmentStateNames.Approved);
}

public sealed class PcScheduleHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<PcScheduleHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    protected override string JobName => PlatformJobNames.PcSchedule;

    protected override TimeSpan Interval => TickInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        scopedServices.GetRequiredService<PcSchedule>().RunOnceAsync(cancellationToken);
}
