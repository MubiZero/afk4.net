using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public sealed record SessionStartStage(
    SessionCommandServiceResult Result,
    Guid? DeviceId,
    DeviceCommandDto? Command);

public interface ISessionStartWorkflow
{
    Task<SessionStartStage> StageAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        bool actorCanApproveComp,
        CancellationToken cancellationToken);

    Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken);
}
