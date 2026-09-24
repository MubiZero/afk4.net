using System.Diagnostics;
using System.Globalization;

namespace AFK4.Agent.Service.Enforcement;

public enum MachinePowerAction
{
    Restart,
    Shutdown
}

public interface IMachinePowerController
{
    /// <summary>
    /// Windows начнёт через <paramref name="delay"/>: за это время агент успевает отправить серверу
    /// ответ, а игрок — прочитать строку Windows о причине.
    /// </summary>
    void Schedule(MachinePowerAction action, TimeSpan delay, string reason);
}

/// <summary>Перезагрузка и выключение через shutdown.exe — тем же путём, что у администратора Windows.</summary>
public sealed class WindowsMachinePowerController : IMachinePowerController
{
    public void Schedule(MachinePowerAction action, TimeSpan delay, string reason)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Power commands run on Windows only.");
        }

        var startInfo = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(action == MachinePowerAction.Restart ? "/r" : "/s");
        // С задержкой больше нуля Windows сама закрывает программы, не спрашивая про несохранённое.
        startInfo.ArgumentList.Add("/t");
        startInfo.ArgumentList.Add(((int)delay.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(reason);
        // Плановая, «прочее»: в журнале Windows это не авария.
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("p:0:0");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("shutdown.exe did not start.");
        if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("shutdown.exe did not answer in time.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"shutdown.exe exited with code {process.ExitCode}.");
        }
    }
}
