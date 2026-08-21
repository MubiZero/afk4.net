using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public sealed record SessionStartStage(
    SessionCommandServiceResult Result,
    Guid? DeviceId,
    DeviceCommandDto? Command);

public interface ISessionStartWorkflow
{
    /// <param name="origin">
    /// Откуда взялась сессия (<see cref="SessionOriginNames"/>). Решает вызывающий, а не запрос:
    /// поле, которое присылает клиент, — это не факт, а заявление, и приложение назвалось бы
    /// стойкой. Умолчания у параметра нет намеренно: забытый аргумент обязан ломать сборку, а не
    /// тихо записывать «посадил оператор».
    /// </param>
    Task<SessionStartStage> StageAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        bool actorCanApproveComp,
        string origin,
        CancellationToken cancellationToken);

    Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken);
}
