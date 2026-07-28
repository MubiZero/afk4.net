using System.IO;
using System.Text.Json;

namespace AFK4.OrganizationAdmin.App.FloorMap;

/// <summary>
/// File-backed <see cref="IActionOutbox"/> stored under the Organization Admin app-data directory. Mirrors the
/// atomic-write + corrupt-self-heal durability of the agent's command-result outbox: writes go through a
/// temp file, and a parse failure deletes the poisoned file and degrades to an empty queue.
/// </summary>
public sealed class FileActionOutbox : IActionOutbox
{
    public const string OutboxFileName = "action-outbox.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string stateDirectory;
    private readonly string outboxFilePath;
    private readonly List<OperatorActionOutboxEntry> pending;

    public FileActionOutbox()
        : this(ResolveDefaultStateDirectory())
    {
    }

    public FileActionOutbox(string stateDirectory)
    {
        this.stateDirectory = stateDirectory;
        outboxFilePath = Path.Combine(stateDirectory, OutboxFileName);
        pending = Load();
    }

    public IReadOnlyList<OperatorActionOutboxEntry> Pending
    {
        get
        {
            lock (syncRoot)
            {
                return pending.ToArray();
            }
        }
    }

    public void Enqueue(OperatorActionOutboxEntry entry)
    {
        lock (syncRoot)
        {
            pending.RemoveAll(existing => existing.DeviceId == entry.DeviceId);
            pending.Add(entry);
            Persist();
        }
    }

    public void Acknowledge(Guid idempotencyKey)
    {
        lock (syncRoot)
        {
            if (pending.RemoveAll(existing => existing.IdempotencyKey == idempotencyKey) > 0)
            {
                Persist();
            }
        }
    }

    private List<OperatorActionOutboxEntry> Load()
    {
        if (!File.Exists(outboxFilePath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(outboxFilePath);
            var entries = JsonSerializer.Deserialize<List<OperatorActionOutboxEntry>>(stream, JsonOptions);
            return entries ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDelete(outboxFilePath);
            return [];
        }
    }

    private void Persist()
    {
        Directory.CreateDirectory(stateDirectory);
        var tempPath = $"{outboxFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, pending, JsonOptions);
            }

            File.Copy(tempPath, outboxFilePath, overwrite: true);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static string ResolveDefaultStateDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "AFK4", "OrganizationAdmin", "cache");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
