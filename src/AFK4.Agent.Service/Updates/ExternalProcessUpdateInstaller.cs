using System.Diagnostics;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class ExternalProcessUpdateInstaller(IOptions<AgentOptions> options) : IUpdateInstallExecutor
{
    public async Task<UpdateInstallResult> ExecuteAsync(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        UpdateInstallState state,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        if (string.IsNullOrWhiteSpace(agentOptions.UpdateInstallerExecutablePath))
        {
            return UpdateInstallResult.Failed("Update installer executable is not configured.");
        }

        if (!File.Exists(agentOptions.UpdateInstallerExecutablePath))
        {
            return UpdateInstallResult.Failed("Configured update installer executable was not found.");
        }

        var arguments = CreateInstallerArguments(agentOptions.UpdateInstallerArgumentsTemplate, instruction, artifact);
        var startInfo = new ProcessStartInfo
        {
            FileName = agentOptions.UpdateInstallerExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return UpdateInstallResult.Failed("Update installer process did not start.");
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

            return UpdateInstallResult.Failed("Update installer timed out.");
        }

        return process.ExitCode == 0
            ? UpdateInstallResult.Success("Update installer completed.")
            : UpdateInstallResult.Failed($"Update installer exited with code {process.ExitCode}.");
    }

    private static string CreateInstallerArguments(
        string template,
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact)
    {
        var argumentsTemplate = string.IsNullOrWhiteSpace(template)
            ? "\"{PackagePath}\" --component \"{Component}\" --version \"{Version}\""
            : template;

        return argumentsTemplate
            .Replace("{PackagePath}", artifact.FilePath, StringComparison.Ordinal)
            .Replace("{Component}", instruction.Component, StringComparison.Ordinal)
            .Replace("{Version}", instruction.Version, StringComparison.Ordinal)
            .Replace("{UpdatePackageId}", instruction.UpdatePackageId.ToString("D"), StringComparison.Ordinal)
            .Replace("{UpdateRolloutId}", instruction.UpdateRolloutId.ToString("D"), StringComparison.Ordinal);
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
