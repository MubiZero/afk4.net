using AFK4.Agent.Service.Logging;
using Microsoft.Extensions.Logging;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Журнал агента живёт на игровой машине месяцами и пишется на каждое сердцебиение. Он не должен
/// ни съесть диск, ни молча потерять строки.
/// </summary>
public sealed class FileLoggerProviderTests
{
    [Fact]
    public void Log_WhenTheFileIsFull_StartsANewOneAndKeepsThePreviousAlongside()
    {
        using var directory = TemporaryDirectory.Create();
        var logPath = Path.Combine(directory.Path, "agent.log");
        var previousPath = Path.Combine(directory.Path, "agent.previous.log");
        using var provider = new FileLoggerProvider(logPath, LogLevel.Information, maxFileBytes: 512);
        var logger = provider.CreateLogger("test");

        var index = 0;
        while (!File.Exists(previousPath) && index < 100)
        {
            logger.LogInformation("Heartbeat {Index} with enough text to fill the tiny log quickly.", index);
            index++;
        }

        logger.LogInformation("Heartbeat after the roll-over.");

        Assert.True(File.Exists(previousPath), "Журнал должен был перевалить за предел и уехать в соседний файл.");
        // Ничего не потеряно: старые строки лежат рядом, а не выброшены. Новый файл начат с нуля.
        Assert.Contains("Heartbeat 0", File.ReadAllText(previousPath), StringComparison.Ordinal);
        Assert.Contains("Heartbeat after the roll-over.", File.ReadAllText(logPath), StringComparison.Ordinal);
        Assert.True(new FileInfo(logPath).Length < 512);
    }

    [Fact]
    public void Log_AfterAFailedWrite_SaysHowManyLinesWereLost()
    {
        using var directory = TemporaryDirectory.Create();
        var logPath = Path.Combine(directory.Path, "agent.log");
        using var provider = new FileLoggerProvider(logPath, LogLevel.Information, maxFileBytes: 1024 * 1024);
        var logger = provider.CreateLogger("test");

        // Каталог занят самим файлом: пока он существует под этим именем как каталог, запись
        // невозможна — так же, как при полном диске или залипшей блокировке.
        Directory.CreateDirectory(logPath);
        logger.LogInformation("This line cannot be written.");
        logger.LogInformation("Neither can this one.");
        Directory.Delete(logPath);

        logger.LogInformation("Writing works again.");

        var text = File.ReadAllText(logPath);
        Assert.Contains("2 log line(s) lost", text, StringComparison.Ordinal);
        Assert.Contains("Writing works again.", text, StringComparison.Ordinal);
    }
}
