using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard;

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

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = currentProcess,
                UseShellExecute = true,
                Verb = "runas"
            });
        }
        catch (Win32Exception exception)
        {
            // Declining the UAC prompt throws ERROR_CANCELLED (1223). Swallow it and report
            // not-elevated so the caller shuts down gracefully instead of crashing.
            SetupWizardStartupLog.Write("Administrator rights are required; elevation was declined or failed.", exception);
        }

        return false;
    }
}
