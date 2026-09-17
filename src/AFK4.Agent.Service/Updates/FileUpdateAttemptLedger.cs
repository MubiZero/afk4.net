using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

/// <summary>
/// Сколько раз этот ПК уже пытался поставить конкретную раскатку.
///
/// Список попыток жил в памяти процесса и обнулялся при каждом перезапуске службы — а перезапуск
/// после обновления штатный. Поэтому сломанный пакет мог крутиться по кругу: поставили — упало —
/// откатились — перезапустились — поставили снова, и так на всём парке сразу.
/// </summary>
public interface IUpdateAttemptLedger
{
    /// <summary>Стоит ли вообще браться за эту раскатку ещё раз.</summary>
    bool ShouldAttempt(Guid rolloutId);

    void RecordAttempt(Guid rolloutId);

    /// <summary>Сбой был временным (сеть, таймаут) — такая попытка не считается.</summary>
    void ForgetAttempt(Guid rolloutId);
}

public sealed class FileUpdateAttemptLedger : IUpdateAttemptLedger
{
    public const string LedgerFileName = "update-attempts.json";

    /// <summary>
    /// Две попытки: первая и одна после перезапуска — ровно столько, чтобы пережить случайный
    /// сбой установки и не начать бесконечный круг на сломанном пакете.
    /// </summary>
    public const int MaxAttempts = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string ledgerPath;
    private readonly Dictionary<Guid, int> attemptsByRollout;

    public FileUpdateAttemptLedger(IOptions<AgentOptions> options)
        : this(options.Value.UpdateStateDirectory)
    {
    }

    public FileUpdateAttemptLedger(string stateDirectory)
    {
        ledgerPath = Path.Combine(stateDirectory, LedgerFileName);
        attemptsByRollout = Load(ledgerPath);
    }

    public bool ShouldAttempt(Guid rolloutId)
    {
        lock (syncRoot)
        {
            return attemptsByRollout.GetValueOrDefault(rolloutId) < MaxAttempts;
        }
    }

    public void RecordAttempt(Guid rolloutId)
    {
        lock (syncRoot)
        {
            attemptsByRollout[rolloutId] = attemptsByRollout.GetValueOrDefault(rolloutId) + 1;
            Persist();
        }
    }

    public void ForgetAttempt(Guid rolloutId)
    {
        lock (syncRoot)
        {
            var attempts = attemptsByRollout.GetValueOrDefault(rolloutId);
            if (attempts <= 1)
            {
                attemptsByRollout.Remove(rolloutId);
            }
            else
            {
                attemptsByRollout[rolloutId] = attempts - 1;
            }

            Persist();
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath)!);
            var tempPath = $"{ledgerPath}.{Guid.NewGuid():N}.tmp";
            var serializable = attemptsByRollout.ToDictionary(pair => pair.Key.ToString("D"), pair => pair.Value);
            File.WriteAllText(tempPath, JsonSerializer.Serialize(serializable, JsonOptions));
            File.Move(tempPath, ledgerPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Не записалось — счёт попыток переживёт только этот запуск. Хуже, чем было, не стало.
        }
    }

    private static Dictionary<Guid, int> Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path), JsonOptions);
            return stored is null
                ? []
                : stored
                    .Where(pair => Guid.TryParse(pair.Key, out _))
                    .ToDictionary(pair => Guid.Parse(pair.Key), pair => pair.Value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}
