using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class InvoiceGenerationHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<BillingOptions> options,
    ILogger<InvoiceGenerationHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly BillingOptions options = options.Value;

    protected override string JobName => PlatformJobNames.InvoiceGeneration;

    protected override TimeSpan Interval => options.GenerationInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var now = GetUtcNow();

        var issued = await scopedServices.GetRequiredService<IInvoiceGenerationRunner>().RunAsync(now, cancellationToken);
        if (issued > 0)
        {
            logger.LogInformation("Invoice generation tick issued {Count} invoice(s).", issued);
        }

        var notified = await scopedServices.GetRequiredService<IDunningRunner>().RunAsync(now, cancellationToken);
        if (notified > 0)
        {
            logger.LogInformation("Dunning tick sent {Count} notice(s).", notified);
        }

        return issued + notified;
    }
}
