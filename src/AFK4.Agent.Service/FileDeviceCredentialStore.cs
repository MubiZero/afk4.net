using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

/// <summary>
/// Ключ этой машины: тот, которым агент подписывает каждый запрос.
///
/// Изначально ключ кладёт в конфиг установщик, но менять его должна уметь сама машина — иначе
/// перевыпуск ключа означает поездку к каждому ПК. Поэтому смененный ключ живёт файлом рядом с
/// остальным состоянием агента (<see cref="AgentOptions.StateDirectory"/>), а конфиг остаётся
/// тем, чем был: значением по умолчанию для первого запуска.
/// </summary>
public interface IDeviceCredentialStore
{
    /// <summary>Действующий ключ: сменённый, если он есть, иначе тот, что положил установщик.</summary>
    string Current { get; }

    /// <summary>Запомнить новый ключ. Пишется атомарно: оборванная запись не оставит половину.</summary>
    void Update(string credentialSecret);
}

public sealed class FileDeviceCredentialStore : IDeviceCredentialStore
{
    public const string CredentialFileName = "device-credential.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string credentialFilePath;
    private readonly string provisionedSecret;
    private string? storedSecret;

    public FileDeviceCredentialStore(IOptions<AgentOptions> options)
        : this(options.Value.StateDirectory, options.Value.DeviceCredentialSecret)
    {
    }

    public FileDeviceCredentialStore(string stateDirectory, string provisionedSecret)
    {
        this.provisionedSecret = provisionedSecret ?? string.Empty;
        credentialFilePath = Path.Combine(stateDirectory, CredentialFileName);
        storedSecret = Load();
    }

    public string Current
    {
        get
        {
            lock (syncRoot)
            {
                return string.IsNullOrWhiteSpace(storedSecret) ? provisionedSecret : storedSecret!;
            }
        }
    }

    public void Update(string credentialSecret)
    {
        if (string.IsNullOrWhiteSpace(credentialSecret))
        {
            // Пустой ключ — это не ключ. Затирать им рабочий значит своими руками отрезать ПК.
            throw new ArgumentException("Device credential secret must not be empty.", nameof(credentialSecret));
        }

        lock (syncRoot)
        {
            WriteAtomically(credentialSecret);
            storedSecret = credentialSecret;
        }
    }

    private string? Load()
    {
        try
        {
            if (!File.Exists(credentialFilePath))
            {
                return null;
            }

            using var stream = File.OpenRead(credentialFilePath);
            var stored = JsonSerializer.Deserialize<StoredCredential>(stream, JsonOptions);
            return string.IsNullOrWhiteSpace(stored?.CredentialSecret) ? null : stored!.CredentialSecret;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Битый файл — не повод остаться без входа: откатываемся на ключ из конфига, а
            // испорченный файл убираем, чтобы он не мешал следующей смене.
            TryDelete(credentialFilePath);
            return null;
        }
    }

    private void WriteAtomically(string credentialSecret)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(credentialFilePath)!);
        var tempPath = $"{credentialFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, new StoredCredential(credentialSecret), JsonOptions);
            }

            File.Copy(tempPath, credentialFilePath, overwrite: true);
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

    private sealed record StoredCredential(string CredentialSecret);
}
