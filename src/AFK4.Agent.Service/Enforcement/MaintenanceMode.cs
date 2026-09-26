using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Обслуживание на агенте (спека оболочки, §6.5): ПК открыт для техника — политики сняты, рабочий
/// стол Windows на экране, игрокам вход закрыт. Правду держит сервер: команда даёт мгновенный
/// отклик, а сердцебиение догоняет пропущенную команду в обе стороны.
/// </summary>
public interface IMaintenanceMode
{
    Task<SessionEnforcementResult> EnterAsync(CancellationToken cancellationToken);

    /// <summary>Вернуть в зал: закрыть рабочий стол техника и вернуть политики и экран «Свободен».</summary>
    Task<SessionEnforcementResult> LeaveAsync(CancellationToken cancellationToken);

    /// <summary>Свести машину с тем, что говорит сервер в сердцебиении.</summary>
    Task ReconcileAsync(bool maintenance, CancellationToken cancellationToken);
}

public sealed class MaintenanceMode(
    IAgentRuntimeStateStore runtimeStateStore,
    IWorkstationLockController workstationLock,
    IMaintenanceDesktop desktop,
    TimeProvider timeProvider,
    ILogger<MaintenanceMode> logger,
    IProtectionEnforcer? protection = null) : IMaintenanceMode
{
    public async Task<SessionEnforcementResult> EnterAsync(CancellationToken cancellationToken)
    {
        var current = runtimeStateStore.Current;
        if (current.State == PlayerShellStateNames.Maintenance)
        {
            return SessionEnforcementResult.Accepted("The PC is already under maintenance.", DeviceCommandOutcomeNames.MaintenanceStarted);
        }

        // Сервер не уводит в обслуживание занятый ПК. Если здесь всё же идёт сессия, агент и сервер
        // разошлись: чужую игру не гасим, а в обслуживание машина уйдёт по сердцебиению после её конца.
        if (current.SessionRuns)
        {
            return SessionEnforcementResult.Rejected("A session is running on this PC.", DeviceCommandOutcomeNames.SessionInProgress);
        }

        // Технику нужен диспетчер задач и всё остальное, что политики прячут от игрока.
        var released = await workstationLock.UnlockAsync(cancellationToken);
        runtimeStateStore.Save(AgentRuntimeState.Maintenance(timeProvider.GetUtcNow(), desktopOpened: false));
        // Профиль защиты тоже снимается: флешка и «Выполнить» технику нужны (§6.5).
        if (protection is not null)
        {
            await protection.ReleaseAsync(cancellationToken);
        }

        var desktopOpened = TryOpenDesktop();
        runtimeStateStore.Save(AgentRuntimeState.Maintenance(timeProvider.GetUtcNow(), desktopOpened));

        return SessionEnforcementResult.Accepted(
            $"Under maintenance; machine policies released ({released.Describe()}); " +
            (desktopOpened ? "Windows desktop opened." : "Windows desktop was already open or could not be opened."),
            DeviceCommandOutcomeNames.MaintenanceStarted);
    }

    public async Task<SessionEnforcementResult> LeaveAsync(CancellationToken cancellationToken)
    {
        var current = runtimeStateStore.Current;
        if (current.State != PlayerShellStateNames.Maintenance)
        {
            return SessionEnforcementResult.Accepted("The PC is back on the floor.", DeviceCommandOutcomeNames.MaintenanceEnded);
        }

        if (current.MaintenanceDesktopOpened)
        {
            TryCloseDesktop();
        }

        runtimeStateStore.MarkLocked(timeProvider.GetUtcNow());
        var locked = await workstationLock.LockAsync(cancellationToken);
        if (protection is not null)
        {
            await protection.ApplyAsync(cancellationToken);
        }

        // Экран «Свободен» вернётся в любом случае, но если политики Windows не встали обратно,
        // администратор должен это прочитать: «в зале» и «в зале, но диспетчер задач открыт» — разное.
        return SessionEnforcementResult.Accepted(
            $"The PC is back on the floor ({locked.Describe()}).",
            locked.IsEnforced ? DeviceCommandOutcomeNames.MaintenanceEnded : DeviceCommandOutcomeNames.MachinePoliciesUnavailable);
    }

    public async Task ReconcileAsync(bool maintenance, CancellationToken cancellationToken)
    {
        var state = runtimeStateStore.Current.State;
        if (maintenance && state == PlayerShellStateNames.Locked)
        {
            logger.LogInformation("The platform has this PC under maintenance; entering it.");
            await EnterAsync(cancellationToken);
        }
        else if (!maintenance && state == PlayerShellStateNames.Maintenance)
        {
            logger.LogInformation("The platform has this PC back on the floor; leaving maintenance.");
            await LeaveAsync(cancellationToken);
        }
    }

    private bool TryOpenDesktop()
    {
        try
        {
            return desktop.Open();
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
        {
            // Без проводника обслуживание всё равно идёт: ПК закрыт для игроков, клавиши свободны.
            logger.LogWarning(exception, "The maintenance desktop could not be opened.");
            return false;
        }
    }

    private void TryCloseDesktop()
    {
        try
        {
            desktop.Close();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning(exception, "The maintenance desktop could not be closed.");
        }
    }
}
