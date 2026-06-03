using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Periodically runs <see cref="AutoProtectionRunner"/> to warn/lock sessions that
/// hit a fixed time-out or an open-tab credit limit.
/// </summary>
public sealed class AutoProtectionHostedService(
    IServiceProvider serviceProvider,
    AutoProtectionOptions options,
    TimeProvider timeProvider,
    ILogger<AutoProtectionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<AutoProtectionRunner>();
                await runner.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auto-protection tick failed.");
            }

            try
            {
                await Task.Delay(options.TickInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
