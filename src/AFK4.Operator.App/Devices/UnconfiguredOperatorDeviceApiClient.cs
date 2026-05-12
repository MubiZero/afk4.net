using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.Devices;

public sealed class UnconfiguredOperatorDeviceApiClient : IOperatorDeviceApiClient
{
    public Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
        Guid branchId,
        Guid organizationId,
        int expiresInSeconds,
        CancellationToken cancellationToken)
    {
        return NotConfigured<DeviceEnrollmentCodeDto>();
    }

    public Task<DeviceCommandDto> DispatchDeviceCommandAsync(
        Guid deviceId,
        string type,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        return NotConfigured<DeviceCommandDto>();
    }

    public Task<DeviceCommandStatusDto> GetDeviceCommandStatusAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        return NotConfigured<DeviceCommandStatusDto>();
    }

    public Task<RotateDeviceCredentialResponse> RotateDeviceCredentialAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        return NotConfigured<RotateDeviceCredentialResponse>();
    }

    public Task<RevokeDeviceCredentialResponse> RevokeDeviceCredentialAsync(
        Guid deviceId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        return NotConfigured<RevokeDeviceCredentialResponse>();
    }

    private static Task<T> NotConfigured<T>()
    {
        throw new InvalidOperationException("Operator device API client is not configured.");
    }
}
