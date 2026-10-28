using System.IO;
using System.Text;
using System.Text.Json;

namespace AFK4.OrganizationAdmin.App.Offline;

/// <summary>
/// Журнал кассы без интернета: каждая операция у стойки — отдельная строка JSON в файле на этом ПК.
/// Хранилище браузера для денег не годится: его чистит обновление WebView2, и журнал пропал бы вместе
/// с ним. Состав записи определяет Панель; здесь только надёжно положить, отдать и убрать отправленное.
/// </summary>
public sealed class OfflineJournal(string path)
{
    /// <summary>Потолок одной записи: сессия или продажа — это сотни байт, а не мегабайты.</summary>
    public const int MaxRecordBytes = 8 * 1024;

    private readonly object syncRoot = new();

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AFK4", "OrganizationAdmin", "offline-journal.jsonl");

    /// <summary>Дописать запись. У записи обязателен строковый <c>id</c>: по нему её потом подтверждают.</summary>
    public void Append(JsonElement record)
    {
        var line = record.GetRawText();
        if (Encoding.UTF8.GetByteCount(line) > MaxRecordBytes
            || record.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(id.GetString()))
        {
            throw new InvalidOperationException("Journal record must be a JSON object with a string id, under 8 KB.");
        }

        lock (syncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Одна запись — одна строка, и после неё сброс на диск: свет в клубе гаснет без предупреждения.
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var bytes = Encoding.UTF8.GetBytes(line.ReplaceLineEndings(" ") + "\n");
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
    }

    public IReadOnlyList<JsonElement> Read()
    {
        lock (syncRoot)
        {
            return ReadUnlocked();
        }
    }

    /// <summary>Убрать записи, которые сервер принял. Остальные остаются в том же порядке.</summary>
    public int Acknowledge(ISet<string> ids)
    {
        lock (syncRoot)
        {
            var kept = ReadUnlocked().Where(record => !ids.Contains(record.GetProperty("id").GetString()!)).ToList();
            var temp = path + ".tmp";
            File.WriteAllLines(temp, kept.Select(record => record.GetRawText()));
            File.Move(temp, path, overwrite: true);
            return kept.Count;
        }
    }

    private List<JsonElement> ReadUnlocked()
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var records = new List<JsonElement>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                records.Add(JsonDocument.Parse(line).RootElement.Clone());
            }
            catch (JsonException)
            {
                // Оборванная последняя строка (свет погас посреди записи) — пропускаем её, остальное цело.
            }
        }

        return records;
    }
}
