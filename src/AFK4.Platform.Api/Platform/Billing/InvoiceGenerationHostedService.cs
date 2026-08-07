using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class InvoiceGenerationHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<BillingOptions> options,
    ILogger<InvoiceGenerationHostedService> logger) : BackgroundService
{
    private readonly BillingOptions options = options.Value;

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
            catch (Exception ex)
            {
                logger.LogError(ex, "Invoice generation tick failed.");
            }

            try
            {
                await Task.Delay(options.GenerationInterval, timeProvider, stoppingToken);
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
        var runner = scope.ServiceProvider.GetRequiredService<IInvoiceGenerationRunner>();
        var now = timeProvider.GetUtcNow();
        var issued = await runner.RunAsync(now, cancellationToken);
        if (issued > 0)
        {
            logger.LogInformation("Invoice generation tick issued {Count} invoice(s).", issued);
        }

        var dunning = scope.ServiceProvider.GetRequiredService<IDunningRunner>();
        var notified = await dunning.RunAsync(now, cancellationToken);
        if (notified > 0)
        {
            logger.LogInformation("Dunning tick sent {Count} notice(s).", notified);
        }
    }
}
