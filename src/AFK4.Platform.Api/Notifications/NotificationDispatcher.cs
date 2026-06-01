using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Hosted background dispatcher. Mirrors <c>InvoiceGenerationHostedService</c>: poll on a fixed
/// interval, and per tick open a scope and run <see cref="NotificationDispatchRunner"/> over the
/// due rows. SMTP latency/outages never block a caller — delivery is fully decoupled (D10).
/// </summary>
public sealed class NotificationDispatcher(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    private readonly NotificationOptions options = options.Value;

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
                logger.LogError(exception, "Notification dispatch tick failed.");
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
        var runner = scope.ServiceProvider.GetRequiredService<NotificationDispatchRunner>();
        var dispatched = await runner.RunAsync(options.DispatchBatchSize, cancellationToken);
        if (dispatched > 0)
        {
            logger.LogInformation("Notification dispatch tick processed {Count} row(s).", dispatched);
        }
    }
}
