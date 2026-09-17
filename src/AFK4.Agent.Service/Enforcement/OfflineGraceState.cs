using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Tracks the inputs the offline grace policy needs: the agent-local time of the last successful
/// backend contact and the effective grace window the backend last advertised. The contact time is
/// stamped by the agent's own clock (spec §8) so the policy is robust to absolute-clock drift on the
/// gaming PC. Before the first successful heartbeat the window falls back to the global default.
/// </summary>
public interface IOfflineGraceState
{
    DateTimeOffset? LastSuccessfulContactUtc { get; }

    int EffectiveGraceMinutes { get; }

    void RecordSuccessfulContact(DateTimeOffset contactAtUtc, int effectiveGraceMinutes);
}

/// <summary>
/// Льгота переживает перезапуск службы.
///
/// Пока это состояние жило только в памяти, перезапуск посреди льготного окна стирал время
/// последнего контакта — и агент терял способность отличить «гость оплатил, а сеть только что
/// пропала» от «связи нет давно». Хранить это на диске дешевле, чем ошибаться в любую сторону:
/// запереть оплаченный ПК или оставить неоплаченный открытым.
/// </summary>
public sealed class OfflineGraceState : IOfflineGraceState
{
    public const string StateFileName = "offline-grace.json";
    private const int DefaultGraceMinutes = 15;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string? stateFilePath;
    private DateTimeOffset? lastSuccessfulContactUtc;
    private int effectiveGraceMinutes = DefaultGraceMinutes;

    /// <summary>Без каталога состояние живёт только в памяти — так его создают тесты.</summary>
    public OfflineGraceState()
    {
    }

    public OfflineGraceState(IOptions<AgentOptions> options)
        : this(options.Value.StateDirectory)
    {
    }

    public OfflineGraceState(string stateDirectory)
    {
        stateFilePath = Path.Combine(stateDirectory, StateFileName);
        var loaded = Load(stateFilePath);
        if (loaded is not null)
        {
            lastSuccessfulContactUtc = loaded.LastSuccessfulContactUtc;
            effectiveGraceMinutes = loaded.EffectiveGraceMinutes;
        }
    }

    public DateTimeOffset? LastSuccessfulContactUtc
    {
        get { lock (syncRoot) { return lastSuccessfulContactUtc; } }
    }

    public int EffectiveGraceMinutes
    {
        get { lock (syncRoot) { return effectiveGraceMinutes; } }
    }

    public void RecordSuccessfulContact(DateTimeOffset contactAtUtc, int effectiveGraceMinutes)
    {
        lock (syncRoot)
        {
            lastSuccessfulContactUtc = contactAtUtc;
            this.effectiveGraceMinutes = effectiveGraceMinutes;
            Persist(contactAtUtc, effectiveGraceMinutes);
        }
    }

    private void Persist(DateTimeOffset contactAtUtc, int graceMinutes)
    {
        if (stateFilePath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
            var tempPath = $"{stateFilePath}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(new PersistedGrace(contactAtUtc, graceMinutes), JsonOptions));
            File.Copy(tempPath, stateFilePath, overwrite: true);
            File.Delete(tempPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Не записалось — значит после перезапуска льготу подтвердить будет нечем и ПК
            // запрётся. Это безопасная сторона ошибки, ронять службу из-за неё незачем.
        }
    }

    private static PersistedGrace? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PersistedGrace>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private sealed record PersistedGrace(DateTimeOffset? LastSuccessfulContactUtc, int EffectiveGraceMinutes);
}
