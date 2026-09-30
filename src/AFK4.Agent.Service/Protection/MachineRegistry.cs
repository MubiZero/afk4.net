using System.Runtime.Versioning;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Protection;

/// <summary>
/// Политики, которые может писать только служба: машинные (HKLM) и игрока (его куст под HKU).
/// Запись игрока идёт только в уже загруженный куст — вошедшего пользователя: подгрузить чужой
/// NTUSER.DAT при его входе значило бы оставить игрока на временном профиле.
/// </summary>
public interface IMachineRegistry
{
    bool IsSupported { get; }

    /// <summary>Есть ли на ПК учётка игрока (мастер записал её SID). Нет киоска — нет и ветки для запретов игроку.</summary>
    bool HasPlayerAccount { get; }

    /// <summary>Игрок вошёл: его куст загружен, и в него можно писать.</summary>
    bool PlayerSignedIn { get; }

    void Write(RegistryWrite write);

    /// <summary>Удалить значение (или подключ списка). Отсутствующее — уже удалено, это не ошибка.</summary>
    void Remove(RegistryWrite write);
}

public sealed partial class WindowsMachineRegistry(IOptions<AgentOptions> options) : IMachineRegistry
{
    public bool IsSupported => OperatingSystem.IsWindows();

    // SID лежит в файле на диске: в путь реестра попадает только разобранный.
    private string? PlayerSid => SecurityIdentifierOrNull(options.Value.ShellPipeClientSid);

    public bool HasPlayerAccount => PlayerSid is not null;

    public bool PlayerSignedIn => OperatingSystem.IsWindows() && PlayerSid is { } sid && PlayerHiveLoaded(sid);

    private static string? SecurityIdentifierOrNull(string? value) =>
        value is not null && SidShape().IsMatch(value.Trim()) ? value.Trim() : null;

    [System.Text.RegularExpressions.GeneratedRegex(@"^S-1-\d+(-\d+)+$")]
    private static partial System.Text.RegularExpressions.Regex SidShape();

    [SupportedOSPlatform("windows")]
    private static bool PlayerHiveLoaded(string sid)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        using var hive = root.OpenSubKey(sid);
        return hive is not null;
    }

    /// <summary>Корень записи: HKLM или куст игрока. Закрывать должен вызывающий.</summary>
    [SupportedOSPlatform("windows")]
    private RegistryKey OpenRoot(PolicyScope scope)
    {
        if (scope == PolicyScope.Machine)
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }

        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        return PlayerSid is { } sid && users.OpenSubKey(sid, writable: true) is { } hive
            ? hive
            : throw new IOException("The player has not signed in: their registry hive is not loaded.");
    }

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
    private void WriteOnWindows(RegistryWrite write)
    {
        using var root = OpenRoot(write.Scope);
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
    private void RemoveOnWindows(RegistryWrite write)
    {
        using var root = OpenRoot(write.Scope);
        if (write.IsList)
        {
            root.DeleteSubKeyTree($@"{write.Key}\{write.Name}", throwOnMissingSubKey: false);
            return;
        }

        using var key = root.OpenSubKey(write.Key, writable: true);
        key?.DeleteValue(write.Name, throwOnMissingValue: false);
    }
}
