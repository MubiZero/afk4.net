using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Reservations;

namespace AFK4.Platform.Api.Reservations;

public sealed record ReservationSessionStartResult(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Code,
    string? Error,
    int? CurrentVersion,
    StartReservationSessionResponse? Response,
    // Заполняется только при отказе по лимиту тарифа: клиент собирает из этих чисел фразу
    // «сеансов 40 из 40», а не показывает голое «нельзя».
    PlanLimitExceededDto? PlanLimit = null)
{
    public static ReservationSessionStartResult Ok(StartReservationSessionResponse response) =>
        new(true, false, false, null, null, null, response);

    public static ReservationSessionStartResult RequestConflict(
        string code,
        string error,
        int? currentVersion = null,
        PlanLimitExceededDto? planLimit = null) =>
        new(false, true, false, code, error, currentVersion, null, planLimit);

    public static ReservationSessionStartResult Missing(string code, string error) =>
        new(false, false, true, code, error, null, null);

    public static ReservationSessionStartResult Invalid(string code, string error) =>
        new(false, false, false, code, error, null, null);
}

public interface IReservationSessionCoordinator
{
    Task<ReservationSessionStartResult> StartAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        bool actorCanApproveComp,
        StartReservationSessionRequest request,
        CancellationToken cancellationToken);
}
