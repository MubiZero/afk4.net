using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed class UpdateRecoveryService(
    IUpdateInstallStateStore stateStore,
    IUpdateRollbackExecutor rollbackExecutor,
    IAgentUpdateClient updateClient,
    TimeProvider timeProvider) : IUpdateRecoveryService
{
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var states = await stateStore.LoadRecoverableAsync(cancellationToken);
        foreach (var state in states)
        {
            var rollbackStarted = state.WithStatus(
                UpdateStatusNames.RollbackStarted,
                "Recovering interrupted update installation.",
                timeProvider.GetUtcNow());
            await stateStore.SaveAsync(rollbackStarted, cancellationToken);
            await ReportAsync(rollbackStarted, cancellationToken);

            var rollbackResult = await rollbackExecutor.RollbackAsync(rollbackStarted, cancellationToken);
            var finalState = rollbackStarted.WithStatus(
                rollbackResult.Succeeded ? UpdateStatusNames.RolledBack : UpdateStatusNames.Failed,
                rollbackResult.Message,
                timeProvider.GetUtcNow());

            await stateStore.SaveAsync(finalState, cancellationToken);
            await ReportAsync(finalState, cancellationToken);
        }
    }

    private Task ReportAsync(UpdateInstallState state, CancellationToken cancellationToken)
    {
        return updateClient.ReportStatusAsync(
            state.UpdateRolloutId,
            state.UpdatePackageId,
            state.Component,
            state.InstalledVersion,
            state.TargetVersion,
            state.Status,
            state.Message,
            state.UpdatedAtUtc,
            cancellationToken);
    }
}
