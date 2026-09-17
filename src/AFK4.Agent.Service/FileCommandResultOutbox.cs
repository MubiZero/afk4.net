using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Logging.Abstractions;
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

    /// <summary>
    /// Сколько ответов держим. Очередь копится, только пока платформа недоступна: за сутки обрыва
    /// команд к одной машине столько не набирается. Предел нужен на случай, когда очередь встала
    /// совсем — файл на игровом ПК не должен расти без конца.
    /// </summary>
    public const int MaxPending = 500;

    private const int PersistAttempts = 3;
    private const int PersistRetryDelayMilliseconds = 50;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string outboxFilePath;
    private readonly List<DeviceCommandResultDto> pending;
    private readonly ILogger<FileCommandResultOutbox> logger;

    public FileCommandResultOutbox(IOptions<AgentOptions> options, ILogger<FileCommandResultOutbox> logger)
        : this(options.Value.StateDirectory, logger)
    {
    }

    public FileCommandResultOutbox(string stateDirectory, ILogger<FileCommandResultOutbox>? logger = null)
    {
        StateDirectory = stateDirectory;
        this.logger = logger ?? NullLogger<FileCommandResultOutbox>.Instance;
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

            // Переполнение означает, что доставка сломана надолго. Выбрасываем самые старые, а не
            // отказываемся принимать новые: свежий ответ полезнее недельной давности. Молча этого
            // не делаем — потеря ответа должна быть видна в журнале машины.
            if (pending.Count > MaxPending)
            {
                var dropped = pending.Count - MaxPending;
                pending.RemoveRange(0, dropped);
                logger.LogWarning(
                    "Command result outbox is full ({MaxPending}). Dropped {DroppedCount} oldest result(s) that the platform never acknowledged.",
                    MaxPending,
                    dropped);
            }

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
            // Битый файл читать нечем, но исчезать он должен со следом: в нём лежали ответы,
            // которых платформа теперь не увидит никогда.
            logger.LogError(
                exception,
                "Command result outbox could not be read and was discarded. Undelivered command results are lost.");
            TryDelete(outboxFilePath);
            return [];
        }
    }

    /// <summary>
    /// Очередь на диск. Сбой записи не выбрасывается наружу: он бы дошёл до исполнения команды и
    /// превратил удачную блокировку ПК в отчёт «не выполнено». В памяти очередь остаётся целой,
    /// и следующая же запись сохранит её.
    /// </summary>
    private void Persist()
    {
        for (var attempt = 1; attempt <= PersistAttempts; attempt++)
        {
            try
            {
                PersistOnce();
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == PersistAttempts)
                {
                    logger.LogError(
                        exception,
                        "Command result outbox could not be saved. The queue is kept in memory and will be saved with the next result.");
                    return;
                }

                // Файл бывает занят на доли секунды — антивирусом, индексатором, предыдущей
                // записью. Это проходит само.
                Thread.Sleep(PersistRetryDelayMilliseconds);
            }
        }
    }

    private void PersistOnce()
    {
        Directory.CreateDirectory(StateDirectory);
        var tempPath = $"{outboxFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, pending, JsonOptions);
            }

            // Переименование, а не копирование: копия может оборваться на середине и оставить
            // обрезанный файл, который при следующем запуске придётся выбросить целиком. Заголовок
            // класса обещает атомарную запись — теперь это правда.
            File.Move(tempPath, outboxFilePath, overwrite: true);
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
