using System.Diagnostics;
using AFK4.GamingPc.Setup.Core;

namespace AFK4.GamingPc.Setup;

public sealed class WindowsServiceController : IWindowsServiceController
{
    public void StopIfExists(string serviceName)
    {
        RunSc("stop", serviceName, allowFailure: true);
    }

    public void Start(string serviceName)
    {
        RunSc("start", serviceName, allowFailure: false);
    }

    public string QueryStatus(string serviceName)
    {
        return RunSc("query", serviceName, allowFailure: false);
    }

    private static string RunSc(string command, string serviceName, bool allowFailure)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"{command} \"{serviceName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start sc.exe.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!allowFailure && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"sc.exe {command} failed with code {process.ExitCode}: {output} {error}");
        }

        return string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim();
    }
}
