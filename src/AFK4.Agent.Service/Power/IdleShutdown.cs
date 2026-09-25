using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Power;

/// <summary>
/// Кто-то за ПК: хост сообщает о вводе не чаще раза в минуту. Служба в сессии 0 ввода игрока не
/// видит — без этого сигнала простой считался бы и тогда, когда человек вводит номер.
/// </summary>
public interface IPlayerPresence
{
    DateTimeOffset? LastSeenUtc { get; }

    void Record(DateTimeOffset at);

    /// <summary>Кто-то тронул ПК — сразу, а не на следующем круге сердцебиения.</summary>
    event Action<DateTimeOffset>? Seen;
}

public sealed class PlayerPresence : IPlayerPresence
{
    private long lastSeenTicks;

    public event Action<DateTimeOffset>? Seen;

    public DateTimeOffset? LastSeenUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref lastSeenTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void Record(DateTimeOffset at)
    {
        Interlocked.Exchange(ref lastSeenTicks, at.UtcTicks);
        Seen?.Invoke(at);
    }
}

public static class IdleShutdownPolicy
{
    /// <summary>Сколько Windows ждёт после назначения: человек успеет тронуть мышь и отменить.</summary>
    public static readonly TimeSpan Warning = TimeSpan.FromSeconds(60);

    /// <summary>Пора ли выключать: ПК свободен и никто не трогал его дольше, чем задал клуб.</summary>
    public static bool ShouldShutDown(int? idleMinutes, DateTimeOffset freeSince, DateTimeOffset? lastSeen, DateTimeOffset now)
    {
        if (idleMinutes is not { } minutes || minutes <= 0)
        {
            return false;
        }

        var quietSince = lastSeen is { } seen && seen > freeSince ? seen : freeSince;
        return now - quietSince >= TimeSpan.FromMinutes(minutes);
    }
}

public interface IIdleShutdownMonitor
{
    /// <summary>Проверить на очередном круге сердцебиения.</summary>
    void Check();

    /// <summary>Когда ПК выключится от простоя; null — не назначено. Экран показывает отсчёт.</summary>
    DateTimeOffset? ShutdownAtUtc { get; }
}

/// <summary>
/// Выключение свободного ПК после простоя (настройки ПК клуба). Только запертый ПК: не в сессии и
/// не на обслуживании. Назначенное выключение отменяется, если человек подошёл в последнюю минуту.
/// Простой считается с перезапуска службы заново — лучше выключить позже, чем под рукой.
/// </summary>
public sealed class IdleShutdownMonitor(
    IAgentRuntimeStateStore runtimeState,
    IProtectionEnforcer protection,
    IPlayerPresence presence,
    IMachinePowerController power,
    TimeProvider timeProvider,
    ILogger<IdleShutdownMonitor> logger,
    AFK4.Agent.Service.Shell.IShellStateSignal? shellState = null) : IIdleShutdownMonitor
{
    private readonly object gate = new();
    private DateTimeOffset? freeSince;
    private DateTimeOffset? scheduledAt;
    private bool subscribed;

    public DateTimeOffset? ShutdownAtUtc
    {
        get
        {
            lock (gate)
            {
                return scheduledAt + IdleShutdownPolicy.Warning;
            }
        }
    }

    public void Check()
    {
        lock (gate)
        {
            if (!subscribed)
            {
                presence.Seen += OnSeen;
                subscribed = true;
            }

            CheckLocked();
        }
    }

    // Человек тронул ПК — выключение, если оно назначено, отменяется сразу.
    private void OnSeen(DateTimeOffset at)
    {
        lock (gate)
        {
            if (scheduledAt is null)
            {
                return;
            }

            TryCancel();
            scheduledAt = null;
            freeSince = at;
        }

        shellState?.Notify();
    }

    private void CheckLocked()
    {
        var now = timeProvider.GetUtcNow();
        if (runtimeState.Current.State != PlayerShellStateNames.Locked)
        {
            // Сессия или обслуживание: простой начнётся заново, когда ПК снова станет свободным.
            freeSince = null;
            scheduledAt = null;
            return;
        }

        freeSince ??= now;
        if (scheduledAt is { } scheduled)
        {
            if (presence.LastSeenUtc is { } seen && seen > scheduled)
            {
                TryCancel();
                scheduledAt = null;
                freeSince = now;
            }

            return;
        }

        var minutes = protection.Profile.IdleShutdownMinutes;
        if (!IdleShutdownPolicy.ShouldShutDown(minutes, freeSince.Value, presence.LastSeenUtc, now))
        {
            return;
        }

        try
        {
            power.Schedule(
                MachinePowerAction.Shutdown,
                IdleShutdownPolicy.Warning,
                $"AFK4: this PC has been free for {minutes} minutes and will turn off. Move the mouse to keep it on.");
            logger.LogInformation("The PC was free for {Minutes} minutes: shutdown scheduled.", minutes);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogWarning(exception, "Idle shutdown could not be scheduled.");
        }

        // Не повторять на каждом круге: и удачное назначение, и отказ ждут, пока ПК не сменит состояние.
        scheduledAt = now;
        shellState?.Notify();
    }

    private void TryCancel()
    {
        try
        {
            power.Cancel();
            logger.LogInformation("Someone is at the PC: the idle shutdown was cancelled.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            logger.LogWarning(exception, "Idle shutdown could not be cancelled.");
        }
    }
}
