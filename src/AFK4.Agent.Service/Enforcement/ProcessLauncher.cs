using System.Diagnostics;

namespace AFK4.Agent.Service.Enforcement;

public sealed class ProcessLauncher : IProcessLauncher
{
    public Task LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
        };

        Process.Start(startInfo);
        return Task.CompletedTask;
    }
}
