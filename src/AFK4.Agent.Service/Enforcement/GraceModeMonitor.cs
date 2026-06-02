namespace AFK4.Agent.Service.Enforcement;

public interface IGraceModeMonitor
{
    Task EnforceAsync(CancellationToken cancellationToken);
}

public sealed class GraceModeMonitor(
    ISessionLeaseStore leaseStore,
    IAgentRuntimeStateStore runtimeStateStore,
    IWorkstationLockController workstationLockController,
    IOfflineLeaseExtender offlineLeaseExtender,
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

        // The signed lease has lapsed, but if the network only just dropped the customer paid for this
        // time — keep the PC unlocked through the grace window (measured from last contact, not from the
        // last lease refresh). The lease is retained so a reconnect can refresh or reconcile it.
        if (offlineLeaseExtender.ShouldExtend(lease, now))
        {
            logger.LogInformation(
                "Session lease {SessionId} lapsed at {ExpiresAtUtc} but is within the offline grace window. Keeping the workstation unlocked.",
                lease.SessionId,
                lease.ExpiresAtUtc);
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
