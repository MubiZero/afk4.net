using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Enforcement;

public interface IRunningProcessTerminator
{
    int TerminateByName(string processName);
}

public sealed class ProcessPolicyEnforcer(
    IOptions<AgentOptions> options,
    IRunningProcessTerminator processTerminator,
    ILogger<ProcessPolicyEnforcer> logger) : IProcessPolicyEnforcer
{
    public AgentLauncherAppOptions? FindAllowedLauncherApp(string appId)
    {
        return options.Value.LauncherApps.FirstOrDefault(app =>
            app.IsEnabled &&
            string.Equals(app.AppId, appId, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(app.ExecutablePath));
    }

    public Task EnforceAsync(CancellationToken cancellationToken)
    {
        foreach (var processName in options.Value.DeniedProcessNames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var terminatedCount = processTerminator.TerminateByName(processName);
            if (terminatedCount > 0)
            {
                logger.LogInformation(
                    "Process policy terminated {TerminatedCount} instance(s) of {ProcessName}.",
                    terminatedCount,
                    processName);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class RunningProcessTerminator : IRunningProcessTerminator
{
    public int TerminateByName(string processName)
    {
        var normalizedName = Path.GetFileNameWithoutExtension(processName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return 0;
        }

        var count = 0;
        foreach (var process in Process.GetProcessesByName(normalizedName))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    count++;
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
            }
        }

        return count;
    }
}
