using System.Diagnostics;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class ExternalProcessAgentRestartScheduler(IOptions<AgentOptions> options) : IAgentRestartScheduler
{
    public async Task<UpdateRestartResult> ScheduleRestartAsync(
        ComponentUpdateInstructionDto instruction,
        UpdateInstallState state,
        CancellationToken cancellationToken)
    {
        if (!RequiresAgentRestart(instruction.Component))
        {
            return UpdateRestartResult.NotRequired("Restart is not required for this component.");
        }

        var agentOptions = options.Value;
        if (string.IsNullOrWhiteSpace(agentOptions.UpdateRestartExecutablePath))
        {
            return UpdateRestartResult.NotRequired("Agent restart executable is not configured.");
        }

        if (!File.Exists(agentOptions.UpdateRestartExecutablePath))
        {
            return UpdateRestartResult.Failed("Configured Agent restart executable was not found.");
        }

        var arguments = CreateArguments(agentOptions.UpdateRestartArgumentsTemplate, state);
        var startInfo = new ProcessStartInfo
        {
            FileName = agentOptions.UpdateRestartExecutablePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return UpdateRestartResult.Failed("Agent restart process did not start.");
        }

        await process.WaitForExitAsync(cancellationToken);

        return process.ExitCode == 0
            ? UpdateRestartResult.Scheduled("Agent restart scheduled.")
            : UpdateRestartResult.Failed($"Agent restart process exited with code {process.ExitCode}.");
    }

    private static string CreateArguments(string template, UpdateInstallState state)
    {
        var argumentsTemplate = string.IsNullOrWhiteSpace(template)
            ? "--component \"{Component}\" --version \"{TargetVersion}\""
            : template;

        return argumentsTemplate
            .Replace("{Component}", state.Component, StringComparison.Ordinal)
            .Replace("{TargetVersion}", state.TargetVersion, StringComparison.Ordinal)
            .Replace("{UpdatePackageId}", state.UpdatePackageId.ToString("D"), StringComparison.Ordinal)
            .Replace("{UpdateRolloutId}", state.UpdateRolloutId.ToString("D"), StringComparison.Ordinal);
    }

    private static bool RequiresAgentRestart(string component)
    {
        return component is
            UpdateComponentNames.AgentService or
            UpdateComponentNames.PlayerShell or
            UpdateComponentNames.OrganizationAdmin;
    }
}
