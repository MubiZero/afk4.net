using AFK4.Shared.Contracts.Platform.Pulse;

namespace AFK4.Platform.Api.Platform.Pulse;

public interface IPlatformPulseService
{
    Task<PlatformPulseDto> GetPulseAsync(CancellationToken cancellationToken);
}
