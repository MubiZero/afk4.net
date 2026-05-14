using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class ExternalProcessUpdateRollbackExecutor(IOptions<AgentOptions> options) : IUpdateRollbackExecutor
{
    public async Task<UpdateRollbackResult> RollbackAsync(
        UpdateInstallState state,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        if (string.IsNullOrWhiteSpace(agentOptions.UpdateRollbackExecutablePath))
        {
            return UpdateRollbackResult.Failed("Update rollback executable is not configured.");
        }

        if (!File.Exists(agentOptions.UpdateRollbackExecutablePath))
        {
            return UpdateRollbackResult.Failed("Configured update rollback executable was not found.");
        }

        var arguments = CreateArguments(agentOptions.UpdateRollbackArgumentsTemplate, state);
        var startInfo = new ProcessStartInfo
        {
            FileName = agentOptions.UpdateRollbackExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return UpdateRollbackResult.Failed("Update rollback process did not start.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, agentOptions.UpdateInstallerTimeoutSeconds)));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);

            return UpdateRollbackResult.Failed("Update rollback process timed out.");
        }

        return process.ExitCode == 0
            ? UpdateRollbackResult.Success("Update rollback completed.")
            : UpdateRollbackResult.Failed($"Update rollback process exited with code {process.ExitCode}.");
    }

    private static string CreateArguments(string template, UpdateInstallState state)
    {
        var argumentsTemplate = string.IsNullOrWhiteSpace(template)
            ? "--component \"{Component}\" --version \"{TargetVersion}\" --artifact \"{ArtifactPath}\""
            : template;

        return argumentsTemplate
            .Replace("{Component}", state.Component, StringComparison.Ordinal)
            .Replace("{TargetVersion}", state.TargetVersion, StringComparison.Ordinal)
            .Replace("{InstalledVersion}", state.InstalledVersion, StringComparison.Ordinal)
            .Replace("{ArtifactPath}", state.ArtifactPath, StringComparison.Ordinal)
            .Replace("{UpdatePackageId}", state.UpdatePackageId.ToString("D"), StringComparison.Ordinal)
            .Replace("{UpdateRolloutId}", state.UpdateRolloutId.ToString("D"), StringComparison.Ordinal);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
