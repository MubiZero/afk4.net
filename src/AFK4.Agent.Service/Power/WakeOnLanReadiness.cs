using System.Management;
using System.Runtime.Versioning;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Power;

/// <summary>
/// Готовит ПК к включению по сети (docs/operations/pc-health.md), чтобы клубу не ходить по ПК с настройками:
/// <list type="bullet">
/// <item>выключает «быстрый запуск» Windows — с ним ПК при выключении засыпает, а не выключается, и многие платы
/// из такого состояния по сети не просыпаются;</item>
/// <item>разрешает сетевой карте будить ПК, и только «волшебным пакетом» — не любым трафиком сети.</item>
/// </list>
/// Пробуждение в BIOS агент включить не может: если ПК всё равно не просыпается, Панель так и скажет.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WakeOnLanReadiness
{
    private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";
    private const string NetClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    /// <param name="macAddress">MAC сетевой карты клуба в виде «AA-BB-CC-DD-EE-FF».</param>
    public static WakeOnLanReadinessDto Ensure(string? macAddress)
    {
        var fastStartupOff = TryOrNull(EnsureFastStartupOff);
        bool? nicWake = null;
        if (macAddress is not null)
        {
            try
            {
                if (FindAdapter(macAddress) is { } adapter)
                {
                    nicWake = EnsureNicWake(adapter.PnpDeviceId, adapter.Guid);
                }
            }
            catch (Exception exception) when (IsSettingFailure(exception))
            {
                nicWake = null;
            }
        }

        return new WakeOnLanReadinessDto(fastStartupOff, nicWake);
    }

    private static bool EnsureFastStartupOff()
    {
        using var key = Registry.LocalMachine.OpenSubKey(PowerKey, writable: true);
        if (key is null)
        {
            return false;
        }

        if (key.GetValue("HiberbootEnabled") is int enabled && enabled == 0)
        {
            return true;
        }

        key.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
        return true;
    }

    private static (string PnpDeviceId, string Guid)? FindAdapter(string macAddress)
    {
        var mac = macAddress.Replace('-', ':');
        using var searcher = new ManagementObjectSearcher(
            $"SELECT PNPDeviceID, GUID FROM Win32_NetworkAdapter WHERE MACAddress = '{mac}' AND PhysicalAdapter = TRUE");
        foreach (var adapter in searcher.Get())
        {
            if (adapter["PNPDeviceID"] is string pnp && adapter["GUID"] is string guid)
            {
                return (pnp, guid);
            }
        }

        return null;
    }

    private static bool EnsureNicWake(string pnpDeviceId, string guid)
    {
        // Ключевое слово драйвера: стандартное *WakeOnMagicPacket. Драйвер читает его при запуске — действует
        // со следующей загрузки, а будить ПК понадобится как раз после выключения.
        using (var adapters = Registry.LocalMachine.OpenSubKey(NetClassKey))
        {
            foreach (var name in adapters?.GetSubKeyNames() ?? [])
            {
                using var adapter = adapters!.OpenSubKey(name, writable: true);
                if (adapter?.GetValue("NetCfgInstanceId") as string is { } id
                    && string.Equals(id, guid, StringComparison.OrdinalIgnoreCase)
                    && adapter.GetValue("*WakeOnMagicPacket") is string current
                    && current != "1")
                {
                    adapter.SetValue("*WakeOnMagicPacket", "1", RegistryValueKind.String);
                }
            }
        }

        // Управление питанием устройства: «разрешить будить компьютер» и «только волшебным пакетом».
        var instance = pnpDeviceId.Replace(@"\", @"\\", StringComparison.Ordinal);
        var allowed = SetWmiFlag("MSPower_DeviceWakeEnable", "Enable", instance);
        SetWmiFlag("MSNdis_DeviceWakeOnMagicPacketOnly", "EnableWakeOnMagicPacketOnly", instance);
        return allowed;
    }

    private static bool SetWmiFlag(string className, string property, string instanceNameStart)
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\wmi", $"SELECT * FROM {className} WHERE InstanceName LIKE '{instanceNameStart}%'");
        var found = false;
        foreach (ManagementObject setting in searcher.Get())
        {
            found = true;
            if (setting[property] is bool on && on)
            {
                continue;
            }

            setting[property] = true;
            setting.Put();
        }

        return found;
    }

    private static T? TryOrNull<T>(Func<T> action) where T : struct
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (IsSettingFailure(exception))
        {
            return null;
        }
    }

    private static bool IsSettingFailure(Exception exception) =>
        exception is ManagementException or UnauthorizedAccessException or System.Security.SecurityException or IOException
            or System.Runtime.InteropServices.COMException;
}
