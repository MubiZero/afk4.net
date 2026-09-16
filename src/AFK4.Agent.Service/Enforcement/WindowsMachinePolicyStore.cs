using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Enforcement;

public sealed class WindowsMachinePolicyStore(ILogger<WindowsMachinePolicyStore> logger) : IMachinePolicyStore
{
    private const string PolicyKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool Set(string valueName, int value)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return Write(valueName, value);
    }

    public bool Remove(string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return Delete(valueName);
    }

    [SupportedOSPlatform("windows")]
    private bool Write(string valueName, int value)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(PolicyKey, writable: true);
            if (key is null)
            {
                logger.LogWarning("Machine policy key could not be opened; '{ValueName}' was not applied.", valueName);
                return false;
            }

            key.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            logger.LogWarning(exception, "Machine policy '{ValueName}' could not be applied.", valueName);
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private bool Delete(string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PolicyKey, writable: true);
            if (key is null)
            {
                // Ключа нет — политики тоже нет. Снимать нечего, и это не ошибка.
                return true;
            }

            key.DeleteValue(valueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            logger.LogWarning(exception, "Machine policy '{ValueName}' could not be removed.", valueName);
            return false;
        }
    }
}
