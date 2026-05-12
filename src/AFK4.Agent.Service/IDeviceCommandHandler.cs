using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public interface IDeviceCommandHandler
{
    Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken);
}
