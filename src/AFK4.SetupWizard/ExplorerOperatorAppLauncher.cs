using System.Diagnostics;
using System.IO;
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard;

// The wizard runs elevated, but the operator must start as the interactive user with the freshly
// written machine environment (AFK4_OPERATOR_*). Launching it as a direct child would inherit the
// wizard's stale, elevated environment — the same trap that left the operator stuck "offline".
// Handing the launch to explorer.exe runs the app de-elevated with the shell's already-refreshed
// environment (from our WM_SETTINGCHANGE broadcast) — exactly like the Start Menu shortcut the
// cashier would otherwise click.
public sealed class ExplorerOperatorAppLauncher : ISetupWizardOperatorLauncher
{
    private const string OperatorAppExeName = "AFK4.Operator.App.exe";
    private const string OperatorAppFolderName = "Operator App";

    public void Launch()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var exePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "AFK4",
            OperatorAppFolderName,
            OperatorAppExeName);

        if (!File.Exists(exePath))
        {
            SetupWizardStartupLog.Write($"Operator app not found at '{exePath}', skipping auto-launch.");
            return;
        }

        var explorerPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "explorer.exe");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = File.Exists(explorerPath) ? explorerPath : "explorer.exe",
                UseShellExecute = false,
                ArgumentList = { exePath }
            });
        }
        catch (Exception exception)
        {
            // A failed auto-launch must not fail the wizard — the install already succeeded and the
            // cashier can still start the operator from the Start Menu shortcut.
            SetupWizardStartupLog.Write("Failed to auto-launch the operator app.", exception);
        }
    }
}
