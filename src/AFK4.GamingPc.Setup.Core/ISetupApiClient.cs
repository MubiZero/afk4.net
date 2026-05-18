using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.GamingPc.Setup.Core;

public interface ISetupApiClient
{
    Task CheckHealthAsync(CancellationToken cancellationToken);

    Task<StaffSignInResponse> SignInAsync(string userName, string password, CancellationToken cancellationToken);

    Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(string accessToken, CancellationToken cancellationToken);

    Task<DeviceEnrollmentResponse> EnrollDeviceAsync(string enrollmentCode, string machineName, CancellationToken cancellationToken);

    Task<DeviceSeatAssignmentDto> AssignDeviceToSmokeSeatAsync(string accessToken, Guid deviceId, CancellationToken cancellationToken);

    Task<DeviceDetailDto> GetDeviceDetailAsync(string accessToken, Guid deviceId, CancellationToken cancellationToken);
}
