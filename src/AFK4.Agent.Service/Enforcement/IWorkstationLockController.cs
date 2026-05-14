namespace AFK4.Agent.Service.Enforcement;

public interface IWorkstationLockController
{
    Task LockAsync(CancellationToken cancellationToken);

    Task UnlockAsync(CancellationToken cancellationToken);
}
