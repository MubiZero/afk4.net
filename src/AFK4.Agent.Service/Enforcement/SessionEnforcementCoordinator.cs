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
            return SessionEnforcementResult.Rejected(validation.Error ?? "Session lease is invalid.");
        }

        leaseStore.Save(lease);
        runtimeStateStore.MarkActive(lease, timeProvider.GetUtcNow());
        await workstationLockController.UnlockAsync(cancellationToken);

        return SessionEnforcementResult.Accepted("Session lease accepted and workstation unlock requested.");
    }

    public Task<SessionEnforcementResult> RefreshLeaseAsync(
        SessionLeaseDto lease,
        CancellationToken cancellationToken)
    {
        var validation = leaseValidator.Validate(lease);
        if (!validation.IsValid)
        {
            return Task.FromResult(SessionEnforcementResult.Rejected(
                validation.Error ?? "Session lease is invalid."));
        }

        leaseStore.Save(lease);
        runtimeStateStore.MarkActive(lease, timeProvider.GetUtcNow());

        return Task.FromResult(SessionEnforcementResult.Accepted("Session lease refreshed."));
    }

    public async Task<SessionEnforcementResult> LockAsync(
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        leaseStore.Clear(sessionId);
        runtimeStateStore.MarkLocked(timeProvider.GetUtcNow());
        await workstationLockController.LockAsync(cancellationToken);

        return SessionEnforcementResult.Accepted("Workstation lock requested.");
    }
}
