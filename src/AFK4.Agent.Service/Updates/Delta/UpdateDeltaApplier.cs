using System.Text.Json;

namespace AFK4.Agent.Service.Updates.Delta;

public sealed record UpdateDeltaJournalEntry(string TargetPath, bool Existed);

/// <summary>Журнал подмены: что собирались заменить и на каком шаге остановились. Лежит в папке состояния обновлений.</summary>
public sealed record UpdateDeltaJournal(
    string Component,
    string Version,
    string State,
    IReadOnlyList<UpdateDeltaJournalEntry> Entries)
{
    public const string Applying = "applying";
    public const string Applied = "applied";
}

/// <summary>
/// Кладёт проверенные файлы из папки подготовки в папку установки. Windows не даёт перезаписать
/// загруженную в процесс DLL, но даёт её переименовать, поэтому каждый файл меняется так:
/// новый копируется рядом как «.afk4new», старый переименовывается в «.afk4old», новый — на его
/// место. Работающая программа держит старые файлы до перезапуска, а следующий запуск видит новые.
/// Любая ошибка возвращает всё уже заменённое назад. Обрыв посреди подмены (сбой питания) разбирает
/// <see cref="RecoverAsync"/> при следующем старте по журналу, записанному до первого файла.
/// </summary>
public static class UpdateDeltaApplier
{
    public const string OldSuffix = ".afk4old";
    public const string NewSuffix = ".afk4new";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task ApplyAsync(
        StagedUpdate staged,
        string component,
        string version,
        string journalPath,
        CancellationToken cancellationToken)
    {
        var entries = staged.Files
            .Select(file => new UpdateDeltaJournalEntry(file.TargetPath, File.Exists(file.TargetPath)))
            .ToList();
        await WriteJournalAsync(journalPath, new UpdateDeltaJournal(component, version, UpdateDeltaJournal.Applying, entries), cancellationToken);

        var done = new List<UpdateDeltaJournalEntry>();
        try
        {
            for (var index = 0; index < staged.Files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = staged.Files[index];
                var entry = entries[index];
                var target = file.TargetPath;

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file.StagedPath, target + NewSuffix, overwrite: true);
                done.Add(entry);
                if (entry.Existed)
                {
                    File.Move(target, target + OldSuffix, overwrite: true);
                }

                File.Move(target + NewSuffix, target);
            }
        }
        catch
        {
            RollBack(done);
            TryDelete(journalPath);
            throw;
        }

        await WriteJournalAsync(journalPath, new UpdateDeltaJournal(component, version, UpdateDeltaJournal.Applied, entries), cancellationToken);
    }

    /// <summary>
    /// Разбор журнала при старте. «applied» — подмена прошла, старые копии больше не держит ни один
    /// процесс: удаляются. «applying» — подмена оборвалась: всё, что успело замениться, возвращается.
    /// </summary>
    /// <returns>Состояние журнала, который был разобран, или null, если журнала не было.</returns>
    public static async Task<string?> RecoverAsync(string journalPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(journalPath))
        {
            return null;
        }

        UpdateDeltaJournal? journal;
        try
        {
            using var stream = File.OpenRead(journalPath);
            journal = await JsonSerializer.DeserializeAsync<UpdateDeltaJournal>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            TryDelete(journalPath);
            return null;
        }

        if (journal is null)
        {
            TryDelete(journalPath);
            return null;
        }

        if (journal.State == UpdateDeltaJournal.Applying)
        {
            RollBack(journal.Entries);
            TryDelete(journalPath);
            return journal.State;
        }

        var leftovers = 0;
        foreach (var entry in journal.Entries.Where(entry => entry.Existed))
        {
            if (!TryDelete(entry.TargetPath + OldSuffix))
            {
                leftovers++;
            }
        }

        if (leftovers == 0)
        {
            TryDelete(journalPath);
        }

        return journal.State;
    }

    private static void RollBack(IEnumerable<UpdateDeltaJournalEntry> entries)
    {
        foreach (var entry in entries.Reverse())
        {
            var target = entry.TargetPath;
            TryDelete(target + NewSuffix);
            if (entry.Existed)
            {
                if (File.Exists(target + OldSuffix))
                {
                    File.Move(target + OldSuffix, target, overwrite: true);
                }
            }
            else
            {
                TryDelete(target);
            }
        }
    }

    private static async Task WriteJournalAsync(string journalPath, UpdateDeltaJournal journal, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        var temp = journalPath + ".tmp";
        await File.WriteAllBytesAsync(temp, JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions), cancellationToken);
        File.Move(temp, journalPath, overwrite: true);
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
