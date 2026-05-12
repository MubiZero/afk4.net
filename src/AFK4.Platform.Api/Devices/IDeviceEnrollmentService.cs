using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceEnrollmentService
{
    Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
        Guid branchId,
        CreateDeviceEnrollmentCodeRequest request,
        CancellationToken cancellationToken);

    Task<DeviceEnrollmentResult> EnrollAsync(DeviceEnrollmentRequest request, CancellationToken cancellationToken);
}
