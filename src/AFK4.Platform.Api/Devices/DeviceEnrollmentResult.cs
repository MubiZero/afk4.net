using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public sealed record DeviceEnrollmentResult(DeviceEnrollmentResponse? Response, string? Error)
{
    public bool Succeeded => Response is not null;

    public static DeviceEnrollmentResult Success(DeviceEnrollmentResponse response)
    {
        return new DeviceEnrollmentResult(response, Error: null);
    }

    public static DeviceEnrollmentResult Failure(string error)
    {
        return new DeviceEnrollmentResult(Response: null, Error: error);
    }
}
