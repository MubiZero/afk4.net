using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Обслуживание на агенте: ПК заперт, экран пишет «на обслуживании», код посадки не показывается.
/// Правду держит сервер: команда даёт мгновенный отклик, а сердцебиение догоняет пропущенную
/// команду в обе стороны.
/// </summary>
public interface IMaintenanceMode
{
    Task<SessionEnforcementResult> EnterAsync(CancellationToken cancellationToken);

    SessionEnforcementResult Leave();

    /// <summary>Свести машину с тем, что говорит сервер в сердцебиении.</summary>
    Task ReconcileAsync(bool maintenance, CancellationToken cancellationToken);
}

public sealed class MaintenanceMode(
    IAgentRuntimeStateStore runtimeStateStore,
    ISessionEnforcementCoordinator enforcementCoordinator,
    TimeProvider timeProvider,
    ILogger<MaintenanceMode> logger) : IMaintenanceMode
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

        var locked = await enforcementCoordinator.LockAsync(sessionId: null, cancellationToken);
        runtimeStateStore.Save(AgentRuntimeState.Maintenance(timeProvider.GetUtcNow()));

        // Экран закрыт в любом случае, но если политики Windows не применились, администратор должен
        // это прочитать: «на обслуживании» и «на обслуживании, но диспетчер задач открыт» — разное.
        return SessionEnforcementResult.Accepted(
            $"Under maintenance; {locked.Message}",
            locked.Outcome == DeviceCommandOutcomeNames.MachinePoliciesUnavailable
                ? DeviceCommandOutcomeNames.MachinePoliciesUnavailable
                : DeviceCommandOutcomeNames.MaintenanceStarted);
    }

    public SessionEnforcementResult Leave()
    {
        if (runtimeStateStore.Current.State == PlayerShellStateNames.Maintenance)
        {
            runtimeStateStore.MarkLocked(timeProvider.GetUtcNow());
        }

        return SessionEnforcementResult.Accepted("The PC is back on the floor.", DeviceCommandOutcomeNames.MaintenanceEnded);
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
            Leave();
        }
    }
}
