using Microsoft.Win32;
using System.Runtime.Versioning;

namespace AFK4.Agent.Service;

public sealed class WindowsInstalledAppInventoryCollector : IInstalledAppInventoryCollector
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Wow6432UninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public Task<IReadOnlyCollection<InstalledAppSnapshot>> CollectAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyCollection<InstalledAppSnapshot>>([]);
        }

        var entries = new List<InstalledAppRegistryEntry>();
        ReadEntries(Registry.LocalMachine, UninstallKey, entries, cancellationToken);
        ReadEntries(Registry.LocalMachine, Wow6432UninstallKey, entries, cancellationToken);
        ReadEntries(Registry.CurrentUser, UninstallKey, entries, cancellationToken);

        return Task.FromResult(InstalledAppRegistryEntryMapper.Map(entries));
    }

    [SupportedOSPlatform("windows")]
    private static void ReadEntries(
        RegistryKey root,
        string path,
        ICollection<InstalledAppRegistryEntry> entries,
        CancellationToken cancellationToken)
    {
        using var uninstallKey = root.OpenSubKey(path);
        if (uninstallKey is null)
        {
            return;
        }

        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var appKey = uninstallKey.OpenSubKey(subKeyName);
            if (appKey is null)
            {
                continue;
            }

            entries.Add(new InstalledAppRegistryEntry(
                DisplayName: appKey.GetValue("DisplayName") as string,
                DisplayVersion: appKey.GetValue("DisplayVersion") as string,
                Publisher: appKey.GetValue("Publisher") as string,
                InstallLocation: appKey.GetValue("InstallLocation") as string,
                InstallDate: appKey.GetValue("InstallDate") as string));
        }
    }
}
