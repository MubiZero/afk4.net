using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceHeartbeatService
{
    Task<DeviceHeartbeatResponse> RecordHeartbeatAsync(Guid deviceId, DeviceHeartbeatRequest request, CancellationToken cancellationToken);
}
