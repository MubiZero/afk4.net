using AFK4.Shared.Contracts.Platform.Analytics;

namespace AFK4.Platform.Api.Platform.Analytics;

public interface IPlatformAnalyticsService
{
    Task<PlatformAnalyticsOverviewDto> GetOverviewAsync(int months, CancellationToken cancellationToken);
}
