using System.Diagnostics;
using System.Security.Principal;

namespace AFK4.GamingPc.Setup;

public static class ElevationGuard
{
    public static bool EnsureElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            return true;
        }

        var currentProcess = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentProcess))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = currentProcess,
            UseShellExecute = true,
            Verb = "runas"
        });

        return false;
    }
}
