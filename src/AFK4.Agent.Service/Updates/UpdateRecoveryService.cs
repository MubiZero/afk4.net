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

            if (IsTargetSuperseded(installedVersion, state.TargetVersion))
            {
                var superseded = state
                    .WithInstalledVersion(installedVersion)
                    .WithStatus(
                        UpdateStatusNames.Superseded,
                        "Recoverable update was superseded by a newer installed version.",
                        timeProvider.GetUtcNow());

                await stateStore.SaveAsync(superseded, cancellationToken);
                await ReportAsync(superseded, cancellationToken);
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

    private static bool IsTargetSuperseded(string installedVersion, string targetVersion)
    {
        return TryParseWindowsInstallerVersion(installedVersion, out var installed) &&
            TryParseWindowsInstallerVersion(targetVersion, out var target) &&
            CompareVersionParts(installed, target) > 0;
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

    private static bool TryParseWindowsInstallerVersion(string version, out int[] parts)
    {
        var normalized = GetWindowsInstallerProductVersion(version);
        var split = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0 || split.Length > 4)
        {
            parts = [];
            return false;
        }

        parts = new int[4];
        for (var index = 0; index < split.Length; index++)
        {
            if (!int.TryParse(split[index], out var part) || part < 0)
            {
                parts = [];
                return false;
            }

            parts[index] = part;
        }

        return true;
    }

    private static int CompareVersionParts(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        for (var index = 0; index < 4; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
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
