using System.Collections.Concurrent;
using System.Text;

namespace AFK4.Agent.Service.Logging;

/// <summary>
/// Minimal append-only file logger. The Agent runs as a Windows Service whose console
/// output is invisible and whose Event Log entries are Warning+ only, so field
/// diagnostics (e.g. why the Player Shell did or did not launch) need a readable file.
/// Writes to %ProgramData%\AFK4\logs\agent.log. Logging failures are swallowed so they
/// can never take down the service — but they are counted, and the count goes into the log
/// as soon as writing works again.
///
/// Файл не растёт бесконечно: на игровой машине служба живёт месяцами, а запись идёт на
/// каждое сердцебиение. Дойдя до предела, журнал уезжает в agent.previous.log, и новый
/// начинается с нуля — на диске остаются последние два.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const long DefaultMaxFileBytes = 8L * 1024 * 1024;

    private readonly string filePath;
    private readonly string previousFilePath;
    private readonly long maxFileBytes;
    private readonly LogLevel minLevel;
    private readonly object gate = new();
    private readonly ConcurrentDictionary<string, FileLogger> loggers = new(StringComparer.Ordinal);
    private int droppedLines;

    public FileLoggerProvider(string filePath, LogLevel minLevel = LogLevel.Information)
        : this(filePath, minLevel, DefaultMaxFileBytes)
    {
    }

    public FileLoggerProvider(string filePath, LogLevel minLevel, long maxFileBytes)
    {
        this.filePath = filePath;
        this.minLevel = minLevel;
        this.maxFileBytes = maxFileBytes;
        previousFilePath = Path.Combine(
            Path.GetDirectoryName(filePath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(filePath)}.previous{Path.GetExtension(filePath)}");

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch
        {
            // Best-effort: if the log directory cannot be created, logging is simply a no-op.
        }
    }

    public static string DefaultLogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "logs",
        "agent.log");

    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() => loggers.Clear();

    internal bool IsEnabled(LogLevel level) => level >= minLevel && level != LogLevel.None;

    internal void Append(string line)
    {
        lock (gate)
        {
            try
            {
                RollOverIfFull();

                if (droppedLines > 0)
                {
                    // Потерянные строки не должны исчезнуть молча: диск был полон или файл был
                    // занят, и в журнале останется дыра. Пусть она будет подписана.
                    var lost = droppedLines;
                    File.AppendAllText(
                        filePath,
                        $"--- {lost} log line(s) lost: the log file could not be written ---{Environment.NewLine}",
                        Encoding.UTF8);
                    droppedLines -= lost;
                }

                File.AppendAllText(filePath, line, Encoding.UTF8);
            }
            catch
            {
                // Сбой записи в журнал не должен ронять службу — но и притворяться, что строка
                // записалась, тоже нельзя.
                droppedLines++;
            }
        }
    }

    private void RollOverIfFull()
    {
        var current = new FileInfo(filePath);
        if (!current.Exists || current.Length < maxFileBytes)
        {
            return;
        }

        File.Move(filePath, previousFilePath, overwrite: true);
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var builder = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                .Append(" [").Append(logLevel).Append("] ")
                .Append(category)
                .Append(" - ")
                .Append(message);

            if (exception is not null)
            {
                builder.Append(Environment.NewLine).Append(exception);
            }

            builder.Append(Environment.NewLine);
            provider.Append(builder.ToString());
        }
    }
}
