using System.Text.Json;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Enforcement;

public sealed class FileSessionLeaseStore : ISessionLeaseStore
{
    public const string LeaseFileName = "session-lease.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string leaseFilePath;
    private readonly TimeProvider timeProvider;
    private readonly SessionLeaseValidator? leaseValidator;

    public FileSessionLeaseStore(
        IOptions<AgentOptions> options,
        TimeProvider timeProvider,
        SessionLeaseValidator leaseValidator)
        : this(options.Value.StateDirectory, timeProvider, leaseValidator)
    {
    }

    public FileSessionLeaseStore(
        string stateDirectory,
        TimeProvider timeProvider,
        SessionLeaseValidator? leaseValidator = null)
    {
        StateDirectory = stateDirectory;
        this.timeProvider = timeProvider;
        this.leaseValidator = leaseValidator;
        leaseFilePath = Path.Combine(stateDirectory, LeaseFileName);
        Current = LoadCurrent();
    }

    public string StateDirectory { get; }

    public SessionLeaseDto? Current { get; private set; }

    public void Save(SessionLeaseDto lease)
    {
        lock (syncRoot)
        {
            Directory.CreateDirectory(StateDirectory);
            WriteAtomically(lease);
            Current = lease;
        }
    }

    public void Clear(Guid? sessionId)
    {
        lock (syncRoot)
        {
            if (sessionId is not null && Current?.SessionId != sessionId)
            {
                return;
            }

            Current = null;
            TryDelete(leaseFilePath);
        }
    }

    private SessionLeaseDto? LoadCurrent()
    {
        if (!File.Exists(leaseFilePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(leaseFilePath);
            var lease = JsonSerializer.Deserialize<SessionLeaseDto>(stream, JsonOptions);
            if (lease is null || lease.ExpiresAtUtc <= timeProvider.GetUtcNow())
            {
                TryDelete(leaseFilePath);
                return null;
            }

            if (leaseValidator is not null && !leaseValidator.Validate(lease).IsValid)
            {
                TryDelete(leaseFilePath);
                return null;
            }

            return lease;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            TryDelete(leaseFilePath);
            return null;
        }
    }

    private void WriteAtomically(SessionLeaseDto lease)
    {
        var tempPath = $"{leaseFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, lease, JsonOptions);
            }

            File.Move(tempPath, leaseFilePath, overwrite: true);
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
