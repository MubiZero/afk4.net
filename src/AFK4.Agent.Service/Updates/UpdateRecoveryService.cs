using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed class UpdateRecoveryService(
    IUpdateInstallStateStore stateStore,
    IUpdateRollbackExecutor rollbackExecutor,
    IAgentUpdateClient updateClient,
    IAgentComponentVersionProvider componentVersionProvider,
    TimeProvider timeProvider) : IUpdateRecoveryService
{
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var states = await stateStore.LoadRecoverableAsync(cancellationToken);
        foreach (var state in states)
        {
            var installedVersion = GetInstalledVersion(state.Component);
            if (IsTargetInstalled(installedVersion, state.TargetVersion))
            {
                var installed = state
                    .WithInstalledVersion(installedVersion)
                    .WithStatus(
                        UpdateStatusNames.Installed,
                        "Interrupted update completed before Agent restart.",
                        timeProvider.GetUtcNow());

                await stateStore.SaveAsync(installed, cancellationToken);
                await ReportAsync(installed, cancellationToken);
                continue;
            }

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

    private string GetInstalledVersion(string component)
    {
        return componentVersionProvider
            .GetInstalledComponents()
            .FirstOrDefault(candidate => string.Equals(candidate.Component, component, StringComparison.Ordinal))
            ?.Version ?? "unknown";
    }

    private static bool IsTargetInstalled(string installedVersion, string targetVersion)
    {
        if (string.Equals(installedVersion, targetVersion, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(
            installedVersion,
            GetWindowsInstallerProductVersion(targetVersion),
            StringComparison.Ordinal);
    }

    private static string GetWindowsInstallerProductVersion(string version)
    {
        var trimmed = version.Trim();
        var length = 0;
        var dotCount = 0;

        while (length < trimmed.Length)
        {
            var character = trimmed[length];
            if (char.IsDigit(character))
            {
                length++;
                continue;
            }

            if (character == '.' && dotCount < 3)
            {
                dotCount++;
                length++;
                continue;
            }

            break;
        }

        while (length > 0 && trimmed[length - 1] == '.')
        {
            length--;
        }

        return length == 0 ? trimmed : trimmed[..length];
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
