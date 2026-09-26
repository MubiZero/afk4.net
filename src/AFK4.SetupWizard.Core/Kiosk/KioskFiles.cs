using System.Text.Json;

namespace AFK4.SetupWizard.Core.Kiosk;

/// <summary>
/// Что было до киоска — файлом рядом с ключом устройства. В нём нет секретов (пароль учётки не
/// хранится нигде, кроме LSA), но править его может только администратор: иначе откат вернул бы
/// параметры, которые подсунул кто-то другой.
/// </summary>
public sealed class FileKioskStateStore(string? filePath = null, bool restrictAccess = true) : IKioskStateStore
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AFK4", "SetupWizard", "kiosk-state.json");

    private readonly string path = filePath ?? DefaultPath;

    public KioskState? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<KioskState>(File.ReadAllText(path));
    }

    public void Save(KioskState state)
    {
        KioskFile.Write(path, JsonSerializer.Serialize(state, KioskFile.Json), restrictAccess);
    }

    public void Clear()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

/// <summary>
/// SID учётки игрока для агента — отдельным файлом рядом с bootstrap.json. Агент читает его вместе
/// с остальной настройкой; откат просто удаляет файл, не трогая настройку с ключом устройства.
/// </summary>
public sealed class FileKioskAgentConfig(string? filePath = null, bool restrictAccess = true) : IKioskAgentConfig
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AFK4", "Agent", "kiosk.json");

    private readonly string path = filePath ?? DefaultPath;

    public void Write(string playerSid)
    {
        var document = new Dictionary<string, object>
        {
            ["Agent"] = new Dictionary<string, string> { ["ShellPipeClientSid"] = playerSid }
        };
        KioskFile.Write(path, JsonSerializer.Serialize(document, KioskFile.Json), restrictAccess);
    }

    public void Clear()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

internal static class KioskFile
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static void Write(string path, string json, bool restrictAccess)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json);
        if (restrictAccess)
        {
            RestrictedFile.RestrictToSystemAndAdministrators(
                path,
                exception => SetupWizardStartupLog.Write($"Could not restrict access to '{path}'.", exception));
        }
    }
}
