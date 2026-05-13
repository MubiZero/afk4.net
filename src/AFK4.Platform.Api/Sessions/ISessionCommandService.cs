using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public sealed record SessionCommandServiceResult(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    SessionCommandResponse? Response)
{
    public static SessionCommandServiceResult Ok(SessionCommandResponse response) => new(true, false, false, null, response);

    public static SessionCommandServiceResult RequestConflict(string error) => new(false, true, false, error, null);

    public static SessionCommandServiceResult Missing(string error) => new(false, false, true, error, null);

    public static SessionCommandServiceResult Invalid(string error) => new(false, false, false, error, null);
}

public interface ISessionCommandService
{
    Task<SessionCommandServiceResult> StartGuestSessionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        CancellationToken cancellationToken);

    Task<SessionCommandServiceResult> ExtendSessionAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        ExtendSessionRequest request,
        CancellationToken cancellationToken);

    Task<SessionCommandServiceResult> TransferSessionAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        TransferSessionRequest request,
        CancellationToken cancellationToken);

    Task<SessionCommandServiceResult> EndSessionAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        EndSessionRequest request,
        CancellationToken cancellationToken);
}
