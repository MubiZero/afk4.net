using Microsoft.Win32;

namespace AFK4.SetupWizard;

public static class SetupWizardFirstRunRegistration
{
    private const string SetupWizardKeyPath = @"Software\AFK4\SetupWizard";
    private const string RunOnceKeyPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string FirstRunPendingValueName = "FirstRunPending";
    private const string RunOnceValueName = "AFK4 Setup Wizard";

    public static void Clear()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        DeleteMachineValue(SetupWizardKeyPath, FirstRunPendingValueName);
        DeleteMachineValue(RunOnceKeyPath, RunOnceValueName);
    }

    private static void DeleteMachineValue(string keyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
