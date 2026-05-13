using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.Devices;

public interface IOperatorDeviceApiClient
{
    Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
        Guid branchId,
        Guid organizationId,
        int expiresInSeconds,
        CancellationToken cancellationToken);

    Task<DeviceCommandDto> DispatchDeviceCommandAsync(
        Guid deviceId,
        string type,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken);

    Task<DeviceCommandStatusDto> GetDeviceCommandStatusAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken);

    Task<DeviceDetailDto> GetDeviceDetailAsync(
        Guid deviceId,
        CancellationToken cancellationToken);

    Task<RotateDeviceCredentialResponse> RotateDeviceCredentialAsync(
        Guid deviceId,
        CancellationToken cancellationToken);

    Task<RevokeDeviceCredentialResponse> RevokeDeviceCredentialAsync(
        Guid deviceId,
        Guid credentialId,
        CancellationToken cancellationToken);
}
