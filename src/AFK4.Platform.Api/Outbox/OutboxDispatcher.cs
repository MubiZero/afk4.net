using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Outbox;

/// <summary>
/// Hosted background dispatcher for the billing outbox. Mirrors <c>NotificationDispatcher</c>: poll on
/// a fixed interval, and per tick open a scope and run <see cref="OutboxDispatchRunner"/> over the due
/// rows. Downstream effects (lock notify, settlement confirmation) are decoupled from the committing
/// command, so a slow/failing effect never blocks or unwinds the money write.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly OutboxOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Billing outbox dispatch tick failed.");
            }

            try
            {
                await Task.Delay(options.PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<OutboxDispatchRunner>();
        var dispatched = await runner.RunAsync(options.DispatchBatchSize, cancellationToken);
        if (dispatched > 0)
        {
            logger.LogInformation("Billing outbox dispatch tick processed {Count} row(s).", dispatched);
        }
    }
}
