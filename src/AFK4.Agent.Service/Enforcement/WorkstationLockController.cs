namespace AFK4.Agent.Service.Enforcement;

public sealed class WorkstationLockController(ILogger<WorkstationLockController> logger) : IWorkstationLockController
{
    public Task LockAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Workstation lock requested by Agent enforcement coordinator.");
        return Task.CompletedTask;
    }

    public Task UnlockAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Workstation unlock requested by Agent enforcement coordinator.");
        return Task.CompletedTask;
    }
}
