using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Protection;

/// <summary>Политики машины в HKLM — то, что может писать только служба.</summary>
public interface IMachineRegistry
{
    bool IsSupported { get; }

    void Write(RegistryWrite write);

    /// <summary>Удалить значение (или подключ списка). Отсутствующее — уже удалено, это не ошибка.</summary>
    void Remove(RegistryWrite write);
}

public sealed class WindowsMachineRegistry : IMachineRegistry
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public void Write(RegistryWrite write)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Machine policies need Windows.");
        }

        WriteOnWindows(write);
    }

    public void Remove(RegistryWrite write)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Machine policies need Windows.");
        }

        RemoveOnWindows(write);
    }

    [SupportedOSPlatform("windows")]
    private static void WriteOnWindows(RegistryWrite write)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        if (write.List is { } list)
        {
            // Список пишется заново целиком: иначе удалённый в Панели сайт остался бы пунктом «5».
            root.DeleteSubKeyTree($@"{write.Key}\{write.Name}", throwOnMissingSubKey: false);
            using var listKey = root.CreateSubKey($@"{write.Key}\{write.Name}", writable: true);
            for (var index = 0; index < list.Count; index++)
            {
                listKey.SetValue((index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), list[index], RegistryValueKind.String);
            }

            return;
        }

        using var key = root.CreateSubKey(write.Key, writable: true);
        if (write.Number is { } number)
        {
            key.SetValue(write.Name, number, RegistryValueKind.DWord);
        }
        else
        {
            key.SetValue(write.Name, write.Text ?? string.Empty, RegistryValueKind.String);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveOnWindows(RegistryWrite write)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        if (write.IsList)
        {
            root.DeleteSubKeyTree($@"{write.Key}\{write.Name}", throwOnMissingSubKey: false);
            return;
        }

        using var key = root.OpenSubKey(write.Key, writable: true);
        key?.DeleteValue(write.Name, throwOnMissingValue: false);
    }
}
