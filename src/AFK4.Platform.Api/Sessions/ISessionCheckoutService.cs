using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public sealed record SessionCheckoutResult(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    SessionCheckoutResponse? Response)
{
    public static SessionCheckoutResult Ok(SessionCheckoutResponse response) => new(true, false, false, null, response);

    public static SessionCheckoutResult RequestConflict(string error) => new(false, true, false, error, null);

    public static SessionCheckoutResult Missing(string error) => new(false, false, true, error, null);

    public static SessionCheckoutResult Invalid(string error) => new(false, false, false, error, null);
}

public interface ISessionCheckoutService
{
    Task<SessionCheckoutResult> CheckoutAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionCheckoutRequest request,
        CancellationToken cancellationToken);
}
