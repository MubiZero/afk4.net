using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

/// <summary>
/// File-backed <see cref="ICommandResultOutbox"/> mirroring <c>FileSessionLeaseStore</c>'s atomic-write
/// durability: the queue is persisted under <see cref="AgentOptions.StateDirectory"/> so a crash or restart
/// between command execution and delivery never loses the result.
/// </summary>
public sealed class FileCommandResultOutbox : ICommandResultOutbox
{
    public const string OutboxFileName = "command-result-outbox.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string outboxFilePath;
    private readonly List<DeviceCommandResultDto> pending;

    public FileCommandResultOutbox(IOptions<AgentOptions> options)
        : this(options.Value.StateDirectory)
    {
    }

    public FileCommandResultOutbox(string stateDirectory)
    {
        StateDirectory = stateDirectory;
        outboxFilePath = Path.Combine(stateDirectory, OutboxFileName);
        pending = LoadPending();
    }

    public string StateDirectory { get; }

    public IReadOnlyList<DeviceCommandResultDto> Pending
    {
        get
        {
            lock (syncRoot)
            {
                return pending.ToArray();
            }
        }
    }

    public void Enqueue(DeviceCommandResultDto result)
    {
        lock (syncRoot)
        {
            pending.RemoveAll(existing => existing.CommandId == result.CommandId);
            pending.Add(result);
            Persist();
        }
    }

    public void Acknowledge(Guid commandId)
    {
        lock (syncRoot)
        {
            if (pending.RemoveAll(existing => existing.CommandId == commandId) > 0)
            {
                Persist();
            }
        }
    }

    private List<DeviceCommandResultDto> LoadPending()
    {
        if (!File.Exists(outboxFilePath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(outboxFilePath);
            var loaded = JsonSerializer.Deserialize<List<DeviceCommandResultDto>>(stream, JsonOptions);
            return loaded ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDelete(outboxFilePath);
            return [];
        }
    }

    private void Persist()
    {
        Directory.CreateDirectory(StateDirectory);
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
