using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

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
