using System.Diagnostics;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

public interface IPlayerShellProcessQuery
{
    bool IsRunning(string executablePath);
}

public interface IPlayerShellProcessStarter
{
    void Start(string executablePath, string arguments);
}

public sealed class PlayerShellProcessSupervisor(
    IOptions<AgentOptions> options,
    IPlayerShellProcessQuery processQuery,
    IPlayerShellProcessStarter processStarter,
    ILogger<PlayerShellProcessSupervisor> logger) : IPlayerShellProcessSupervisor
{
    private static readonly HashSet<string> StatesRequiringShell = new(StringComparer.Ordinal)
    {
        PlayerShellStateNames.Locked,
        PlayerShellStateNames.Active,
        PlayerShellStateNames.Grace,
        PlayerShellStateNames.Ending,
        PlayerShellStateNames.Maintenance,
        PlayerShellStateNames.Offline,
        PlayerShellStateNames.Error
    };

    public Task EnsureRunningAsync(AgentRuntimeState runtimeState, CancellationToken cancellationToken)
    {
        if (!StatesRequiringShell.Contains(runtimeState.State))
        {
            return Task.CompletedTask;
        }

        var executablePath = options.Value.PlayerShellExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            logger.LogDebug("Player Shell executable path is not configured.");
            return Task.CompletedTask;
        }

        if (!File.Exists(executablePath))
        {
            logger.LogWarning("Player Shell executable path {ExecutablePath} does not exist.", executablePath);
            return Task.CompletedTask;
        }

        if (processQuery.IsRunning(executablePath))
        {
            return Task.CompletedTask;
        }

        processStarter.Start(executablePath, options.Value.PlayerShellStartArguments);
        logger.LogInformation("Player Shell process start requested for {ExecutablePath}.", executablePath);

        return Task.CompletedTask;
    }
}

public sealed class PlayerShellProcessQuery : IPlayerShellProcessQuery
{
    public bool IsRunning(string executablePath)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var processes = Process.GetProcessesByName(processName);
        try
        {
            foreach (var process in processes)
            {
                if (IsMatchingProcess(process, executablePath))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsMatchingProcess(Process process, string executablePath)
    {
        try
        {
            var processPath = process.MainModule?.FileName;
            return string.Equals(processPath, executablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }
}

public sealed class PlayerShellProcessStarter : IPlayerShellProcessStarter
{
    public void Start(string executablePath, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
        };

        Process.Start(startInfo);
    }
}
