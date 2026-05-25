using System.Diagnostics;
using System.IO;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard;

public sealed class AgentServiceCompletionAction(string serviceName = "AFK4.Agent.Service") : ISetupWizardCompletionAction
{
    private const int ServiceDoesNotExist = 1060;
    private const int ServiceAlreadyRunning = 1056;

    public void Complete()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var queryResult = RunScCommand(["query", serviceName], throwOnFailure: false);
        if (queryResult == ServiceDoesNotExist)
        {
            return;
        }

        RunScCommand(["config", serviceName, "start=", "auto"], throwOnFailure: true);
        var startResult = RunScCommand(["start", serviceName], throwOnFailure: false);
        if (startResult is not 0 and not ServiceAlreadyRunning)
        {
            throw new InvalidOperationException($"AFK4 Agent Service could not be started. sc.exe exited with code {startResult}.");
        }
    }

    private static int RunScCommand(IReadOnlyList<string> arguments, bool throwOnFailure)
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var scPath = string.IsNullOrWhiteSpace(windowsDirectory)
            ? "sc.exe"
            : Path.Combine(windowsDirectory, "System32", "sc.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = scPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("sc.exe could not be started.");
        process.WaitForExit();

        if (throwOnFailure && process.ExitCode != 0)
        {
            var output = (process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd()).Trim();
            throw new InvalidOperationException($"sc.exe {arguments[0]} failed with exit code {process.ExitCode}. {output}");
        }

        return process.ExitCode;
    }
}
