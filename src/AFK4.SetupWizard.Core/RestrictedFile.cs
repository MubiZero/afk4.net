using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Сужение прав на файл с секретом: только система и администраторы.
///
/// Общая для тех мест мастера, которые кладут на диск что-то, чем можно представиться от имени
/// этой машины: ключ устройства и bootstrap.json. Две копии этого кода разъехались бы — одна
/// сужала права, вторая (ключ) не сужала их вовсе.
/// </summary>
public static class RestrictedFile
{
    /// <summary>
    /// Лучшее из возможного: не удалось сузить права — установка всё равно продолжается, но
    /// молчать об этом нельзя. Файл с секретом, оставшийся открытым, должен оставить след.
    /// </summary>
    public static void RestrictToSystemAndAdministrators(string path, Action<Exception>? onFailure = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            RunIcacls(path);
        }
        catch (Exception exception)
        {
            onFailure?.Invoke(exception);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RunIcacls(string path)
    {
        var icacls = Path.Combine(Environment.SystemDirectory, "icacls.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = File.Exists(icacls) ? icacls : "icacls.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("/inheritance:r");
        startInfo.ArgumentList.Add("/grant:r");
        startInfo.ArgumentList.Add("*S-1-5-18:F");      // NT AUTHORITY\SYSTEM
        startInfo.ArgumentList.Add("/grant:r");
        startInfo.ArgumentList.Add("*S-1-5-32-544:F");  // BUILTIN\Administrators

        // Тот, от чьего имени идёт установка. Мастер работает с правами администратора, так что
        // запись обычно повторяет предыдущую. Нужна она для другого: без права перечитать и
        // переписать собственный файл повторный прогон мастера упёрся бы в им же поставленные
        // права и не смог бы переиспользовать ключ.
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            startInfo.ArgumentList.Add("/grant:r");
            startInfo.ArgumentList.Add($"*{currentUser.Value}:F");
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("icacls could not be started.");
        if (!process.WaitForExit(5000))
        {
            throw new TimeoutException("icacls did not finish in time.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"icacls exited with code {process.ExitCode} for '{path}'.");
        }
    }
}
