using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Sessions;

public interface ISessionCommandResultProcessor
{
    Task ProcessAsync(DeviceCommandResultDto result, CancellationToken cancellationToken);
}
