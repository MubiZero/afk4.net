using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceCommandDispatchService
{
    Task<DeviceCommandDto> EnqueueAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken);

    Task NotifyAsync(
        Guid deviceId,
        DeviceCommandDto command,
        CancellationToken cancellationToken);

    Task<DeviceCommandDto> DispatchAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken);
}
