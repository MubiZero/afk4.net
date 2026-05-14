namespace AFK4.Agent.Service.Updates;

public interface IUpdateRollbackExecutor
{
    Task<UpdateRollbackResult> RollbackAsync(
        UpdateInstallState state,
        CancellationToken cancellationToken);
}
