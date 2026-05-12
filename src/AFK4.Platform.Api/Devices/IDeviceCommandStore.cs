using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceCommandStore
{
    Task AddPendingAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken);

    Task ApplyResultAsync(DeviceCommandResultDto result, CancellationToken cancellationToken);

    Task<DeviceCommandStatusDto?> GetAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken);
}
