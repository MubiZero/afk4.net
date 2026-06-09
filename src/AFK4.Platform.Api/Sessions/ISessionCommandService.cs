using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public sealed record SessionCommandServiceResult(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    SessionCommandResponse? Response,
    // Machine-readable conflict reason ("stale_version", "seat_occupied") and, for a stale
    // version, the authoritative current version so the client can refresh and retry.
    string? Code = null,
    int? CurrentVersion = null)
{
    public static SessionCommandServiceResult Ok(SessionCommandResponse response) => new(true, false, false, null, response);

    public static SessionCommandServiceResult RequestConflict(string error, string? code = null) =>
        new(false, true, false, error, null, code);

    public static SessionCommandServiceResult StaleVersion(int currentVersion) =>
        new(false, true, false, "This session changed since you last loaded it; refresh and try again.", null, "stale_version", currentVersion);

    public static SessionCommandServiceResult Missing(string error) => new(false, false, true, error, null);

    public static SessionCommandServiceResult Invalid(string error) => new(false, false, false, error, null);
}

public interface ISessionCommandService
{
    Task<SessionCommandServiceResult> StartGuestSessionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        CancellationToken cancellationToken,
        // Anti-fraud §5.4: whether the actor may authorise a comp above the comp threshold
        // (holds ApproveMoneyAction). Supplied by the endpoint from the staff context.
        bool actorCanApproveComp = false);

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
