namespace AFK4.Agent.Service.Enforcement;

public interface IGraceModeMonitor
{
    Task EnforceAsync(CancellationToken cancellationToken);
}

public sealed class GraceModeMonitor(
    ISessionLeaseStore leaseStore,
    IAgentRuntimeStateStore runtimeStateStore,
    IWorkstationLockController workstationLockController,
    TimeProvider timeProvider,
    ILogger<GraceModeMonitor> logger) : IGraceModeMonitor
{
    public async Task EnforceAsync(CancellationToken cancellationToken)
    {
        var lease = leaseStore.Current;
        if (lease is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (lease.ExpiresAtUtc > now)
        {
            return;
        }

        leaseStore.Clear(lease.SessionId);
        runtimeStateStore.MarkLocked(now);
        await workstationLockController.LockAsync(cancellationToken);

        logger.LogInformation(
            "Session lease {SessionId} expired at {ExpiresAtUtc}. Workstation lock requested.",
            lease.SessionId,
            lease.ExpiresAtUtc);
    }
}
