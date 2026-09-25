using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Enforcement;

public sealed class SessionEnforcementCoordinator(
    SessionLeaseValidator leaseValidator,
    ISessionLeaseStore leaseStore,
    IAgentRuntimeStateStore runtimeStateStore,
    IWorkstationLockController workstationLockController,
    TimeProvider timeProvider) : ISessionEnforcementCoordinator
{
    public async Task<SessionEnforcementResult> UnlockAsync(
        SessionLeaseDto lease,
        CancellationToken cancellationToken)
    {
        var validation = leaseValidator.Validate(lease);
        if (!validation.IsValid)
        {
            return SessionEnforcementResult.Rejected(
                validation.Error ?? "Session lease is invalid.",
                DeviceCommandOutcomeNames.LeaseInvalid);
        }

        leaseStore.Save(lease);
        runtimeStateStore.MarkActive(lease, timeProvider.GetUtcNow());
        var unlock = await workstationLockController.UnlockAsync(cancellationToken);

        return SessionEnforcementResult.Accepted(
            $"Session lease accepted; workstation unlocked ({unlock.Describe()}).",
            unlock.IsEnforced
                ? DeviceCommandOutcomeNames.LeaseAccepted
                : DeviceCommandOutcomeNames.MachinePoliciesUnavailable);
    }

    public Task<SessionEnforcementResult> RefreshLeaseAsync(
        SessionLeaseDto lease,
        CancellationToken cancellationToken)
    {
        var validation = leaseValidator.Validate(lease);
        if (!validation.IsValid)
        {
            return Task.FromResult(SessionEnforcementResult.Rejected(
                validation.Error ?? "Session lease is invalid.",
                DeviceCommandOutcomeNames.LeaseInvalid));
        }

        leaseStore.Save(lease);
        runtimeStateStore.MarkActive(lease, timeProvider.GetUtcNow());

        return Task.FromResult(SessionEnforcementResult.Accepted(
            "Session lease refreshed.",
            DeviceCommandOutcomeNames.LeaseRefreshed));
    }

    public async Task<SessionEnforcementResult> LockAsync(
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        // Обслуживание заканчивает только «Вернуть в зал»: оно же закрывает проводник техника.
        // «Запереть» посреди обслуживания потеряло бы эту память, и проводник остался бы открытым
        // навсегда. А закрыт для игроков такой ПК и так — сервер не начнёт на нём сессию.
        if (runtimeStateStore.Current.State == PlayerShellStateNames.Maintenance)
        {
            return SessionEnforcementResult.Accepted(
                "The PC is under maintenance and already closed to players; return it to the floor to lock it.",
                DeviceCommandOutcomeNames.MaintenanceStarted);
        }

        leaseStore.Clear(sessionId);
        runtimeStateStore.MarkLocked(timeProvider.GetUtcNow());
        var lockOutcome = await workstationLockController.LockAsync(cancellationToken);

        // Оболочка закрывает экран в любом случае — это её работа и она от машинных политик не
        // зависит. А вот что удалось запереть на самой машине, оператор должен прочитать как есть:
        // «заперто» и «заперто, но политики машины не применились» — разные новости.
        return SessionEnforcementResult.Accepted(
            $"Workstation locked ({lockOutcome.Describe()}).",
            lockOutcome.IsEnforced
                ? DeviceCommandOutcomeNames.WorkstationLocked
                : DeviceCommandOutcomeNames.MachinePoliciesUnavailable);
    }
}
