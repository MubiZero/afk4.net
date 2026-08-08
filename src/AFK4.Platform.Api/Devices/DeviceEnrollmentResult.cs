using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Devices;

public sealed record DeviceEnrollmentResult(
    DeviceEnrollmentResponse? Response,
    string? Error,
    PlanLimitExceededDto? PlanLimit = null)
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

    public static DeviceEnrollmentResult PlanLimitReached(PlanLimitExceededDto planLimit) =>
        // Текст здесь — для лога и для старых клиентов; человеку фразу собирает интерфейс из PlanLimit.
        new(Response: null, Error: "Plan device limit for this branch has been reached.", PlanLimit: planLimit);
}
