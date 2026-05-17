using System.Diagnostics;
using System.IO;
using AFK4.GamingPc.Setup.Core;

namespace AFK4.GamingPc.Setup;

public sealed class GamingPcMsiInstaller(SetupEmbeddedResources resources) : IGamingPcMsiInstaller
{
    public async Task InstallAsync(CancellationToken cancellationToken)
    {
        var msiPath = await resources.ExtractMsiAsync(cancellationToken);
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AFK4",
            "Agent",
            "InstallLogs");
        Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(logDirectory, "gaming-pc-bootstrapper-install.log");
        var startInfo = new ProcessStartInfo
        {
            FileName = "msiexec.exe",
            Arguments = $"/i \"{msiPath}\" /qn /norestart /l*v \"{logPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start msiexec.exe.");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"msiexec.exe exited with code {process.ExitCode}. Log: {logPath}");
        }
    }
}
