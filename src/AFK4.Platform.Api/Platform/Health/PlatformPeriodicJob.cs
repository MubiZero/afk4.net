using AFK4.Platform.Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Общий цикл периодического задания. Обёртка одна на всех намеренно: пока каждый сервис писал
/// свой while+Delay+catch, поломка задания видна была только в логе, а седьмое задание просто
/// забыли бы подключить к наблюдению.
/// </summary>
public abstract class PlatformPeriodicJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger logger) : BackgroundService
{
    protected abstract string JobName { get; }

    protected abstract TimeSpan Interval { get; }

    // Routes through the base class instead of subclasses re-capturing the timeProvider
    // constructor parameter themselves, which the compiler flags as a double capture
    // alongside the base(...) call.
    protected DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();

    /// <summary>Одна итерация. Возвращает число обработанных единиц (для строки прогона).</summary>
    protected abstract Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Один тик с записью результата. Открыт для тестов — не требует запуска хоста.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var processed = 0;
        string? error = null;

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            processed = await TickAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            logger.LogError(exception, "Periodic job {JobName} tick failed.", JobName);
        }

        // Запись прогона идёт в собственном scope: у тика мог сдохнуть DbContext,
        // и попытка записаться через него потеряла бы ровно ту запись, ради которой всё делается.
        try
        {
            await using var recordScope = serviceProvider.CreateAsyncScope();
            var recorder = recordScope.ServiceProvider.GetRequiredService<IJobRunRecorder>();
            await recorder.RecordAsync(
                JobName,
                startedAt,
                timeProvider.GetUtcNow(),
                error is null ? PlatformJobOutcomeNames.Succeeded : PlatformJobOutcomeNames.Failed,
                processed,
                error,
                cancellationToken);
        }
        catch (Exception recordException)
        {
            logger.LogError(recordException, "Failed to record run of periodic job {JobName}.", JobName);
        }
    }
}
