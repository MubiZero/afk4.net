using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IBillingMetricsService
{
    Task<PlatformBillingMetricsDto> GetAsync(CancellationToken cancellationToken);
}
