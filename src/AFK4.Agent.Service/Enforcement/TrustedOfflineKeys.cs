using System.Text.Json;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Ключи ПК администраторов филиала, которым агент верит офлайн-аренды. Приходят в ответе на сердцебиение
/// и лежат на диске: без интернета ими и пользуются, а интернета как раз нет.
/// </summary>
public sealed class TrustedOfflineKeys
{
    public const string StateFileName = "offline-lease-keys.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string? stateFilePath;
    private IReadOnlyList<OfflineLeaseKeyDto> keys = [];

    /// <summary>Без каталога ключи живут только в памяти — так их создают тесты.</summary>
    public TrustedOfflineKeys()
    {
    }

    public TrustedOfflineKeys(IOptions<AgentOptions> options)
        : this(options.Value.StateDirectory)
    {
    }

    public TrustedOfflineKeys(string stateDirectory)
    {
        stateFilePath = Path.Combine(stateDirectory, StateFileName);
        try
        {
            if (File.Exists(stateFilePath))
            {
                keys = JsonSerializer.Deserialize<List<OfflineLeaseKeyDto>>(File.ReadAllText(stateFilePath), JsonOptions) ?? [];
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Нечитаемый файл — значит, доверенных ключей нет: запереть лишний раз безопаснее, чем
            // открыть ПК по ключу, которого мы не помним.
            keys = [];
        }
    }

    public IReadOnlyList<OfflineLeaseKeyDto> Current
    {
        get { lock (syncRoot) { return keys; } }
    }

    public string? FindPublicKeyPem(string keyId)
    {
        lock (syncRoot)
        {
            return keys.FirstOrDefault(key => string.Equals(key.KeyId, keyId, StringComparison.Ordinal))?.PublicKeyPem;
        }
    }

    /// <summary>Список с сервера заменяет прежний целиком: отозванный ключ уходит сразу.</summary>
    public void Replace(IReadOnlyList<OfflineLeaseKeyDto> next)
    {
        lock (syncRoot)
        {
            if (keys.SequenceEqual(next))
            {
                return;
            }

            keys = next.ToList();
            if (stateFilePath is not null)
            {
                AtomicFile.WriteAllText(stateFilePath, JsonSerializer.Serialize(keys, JsonOptions));
            }
        }
    }
}
