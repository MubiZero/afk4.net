using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceCredentialLifecycleService
{
    Task<RotateDeviceCredentialResponse?> RotateAsync(Guid deviceId, CancellationToken cancellationToken);

    Task<RevokeDeviceCredentialResponse?> RevokeAsync(Guid deviceId, Guid credentialId, CancellationToken cancellationToken);
}
