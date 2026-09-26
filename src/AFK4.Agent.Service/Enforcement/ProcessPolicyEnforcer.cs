using System.Diagnostics;
using AFK4.Agent.Service.Games;
using AFK4.Agent.Service.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Enforcement;

public interface IRunningProcessTerminator
{
    int TerminateByName(string processName);
}

public sealed class ProcessPolicyEnforcer(
    IOptions<AgentOptions> options,
    IRunningProcessTerminator processTerminator,
    ILogger<ProcessPolicyEnforcer> logger,
    ILauncherCatalog? catalog = null,
    IShellHeartbeatSnapshot? heartbeatSnapshot = null) : IProcessPolicyEnforcer
{
    public AgentLauncherAppOptions? FindAllowedLauncherApp(string appId)
    {
        // Разрешено ровно то, что игрок видит в библиотеке: список один на показ и на запуск. Запертая
        // по возрасту плитка не запускается и в обход экрана.
        if (catalog is not null)
        {
            return catalog.Find(appId) is { } entry
                   && !GameAgeGate.IsLocked(entry.MinAge, heartbeatSnapshot?.SessionOwner?.PlayerAge)
                ? new AgentLauncherAppOptions
                {
                    AppId = entry.AppId,
                    DisplayName = entry.DisplayName,
                    Category = entry.Category,
                    ExecutablePath = entry.ExecutablePath ?? string.Empty,
                    Arguments = entry.Arguments,
                    AllowWithoutSession = entry.AllowWithoutSession
                }
                : null;
        }

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
