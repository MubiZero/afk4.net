using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AFK4.Operator.App.Connection;

public sealed class ProtectedDataOperatorConnectionStore : IOperatorConnectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string path;

    public ProtectedDataOperatorConnectionStore()
        : this(GetDefaultPath())
    {
    }

    public ProtectedDataOperatorConnectionStore(string path)
    {
        this.path = path;
    }

    public async Task SaveAsync(OperatorConnectionSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var protectedBytes = ProtectedData.Protect(json, optionalEntropy: null, DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(path, protectedBytes, cancellationToken);
    }

    public async Task<OperatorConnectionSnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        // DPAPI Unprotect throws on a corrupt/truncated .bin or one written under a different
        // user/machine. Self-heal like FileFloorMapCache: delete the poisoned file and degrade
        // to the sign-in screen rather than crashing the host on every launch.
        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var json = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

            return JsonSerializer.Deserialize<OperatorConnectionSnapshot>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or JsonException)
        {
            TryDelete(path);
            return null;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
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

    private static string GetDefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "AFK4", "Operator", "connection.bin");
    }
}
