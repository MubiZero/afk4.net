using System.Text.Json;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Enforcement;

public sealed class AgentRuntimeStateStore : IAgentRuntimeStateStore
{
    public const string StateFileName = "runtime-state.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string stateFilePath;

    public AgentRuntimeStateStore(IOptions<AgentOptions> options, TimeProvider timeProvider)
        : this(options.Value.StateDirectory, timeProvider)
    {
    }

    public AgentRuntimeStateStore(string stateDirectory, TimeProvider timeProvider)
    {
        StateDirectory = stateDirectory;
        stateFilePath = Path.Combine(stateDirectory, StateFileName);
        Current = LoadOrDefault(timeProvider.GetUtcNow());
    }

    public string StateDirectory { get; }

    public AgentRuntimeState Current { get; private set; }

    public void Save(AgentRuntimeState state)
    {
        lock (syncRoot)
        {
            Directory.CreateDirectory(StateDirectory);
            WriteAtomically(state);
            Current = state;
        }
    }

    public void MarkLocked(DateTimeOffset observedAtUtc)
    {
        Save(AgentRuntimeState.Locked(observedAtUtc));
    }

    public void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc)
    {
        Save(AgentRuntimeState.Active(lease, observedAtUtc));
    }

    private AgentRuntimeState LoadOrDefault(DateTimeOffset now)
    {
        if (!File.Exists(stateFilePath))
        {
            return AgentRuntimeState.Locked(now);
        }

        try
        {
            using var stream = File.OpenRead(stateFilePath);
            return JsonSerializer.Deserialize<AgentRuntimeState>(stream, JsonOptions) ??
                AgentRuntimeState.Locked(now);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDelete(stateFilePath);
            return AgentRuntimeState.Locked(now);
        }
    }

    private void WriteAtomically(AgentRuntimeState state)
    {
        var tempPath = $"{stateFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, state, JsonOptions);
            }

            File.Move(tempPath, stateFilePath, overwrite: true);
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
