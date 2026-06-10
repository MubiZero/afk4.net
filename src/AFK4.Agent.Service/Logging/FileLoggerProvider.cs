using System.Collections.Concurrent;
using System.Text;

namespace AFK4.Agent.Service.Logging;

/// <summary>
/// Minimal append-only file logger. The Agent runs as a Windows Service whose console
/// output is invisible and whose Event Log entries are Warning+ only, so field
/// diagnostics (e.g. why the Player Shell did or did not launch) need a readable file.
/// Writes to %ProgramData%\AFK4\logs\agent.log. Logging failures are swallowed so they
/// can never take down the service.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string filePath;
    private readonly LogLevel minLevel;
    private readonly object gate = new();
    private readonly ConcurrentDictionary<string, FileLogger> loggers = new(StringComparer.Ordinal);

    public FileLoggerProvider(string filePath, LogLevel minLevel = LogLevel.Information)
    {
        this.filePath = filePath;
        this.minLevel = minLevel;

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
        try
        {
            lock (gate)
            {
                File.AppendAllText(filePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Never let a logging failure crash the agent.
        }
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
