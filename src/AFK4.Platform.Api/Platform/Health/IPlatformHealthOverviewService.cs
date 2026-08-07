using AFK4.Shared.Contracts.Platform.Health;

namespace AFK4.Platform.Api.Platform.Health;

public interface IPlatformHealthOverviewService
{
    Task<PlatformHealthOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}
