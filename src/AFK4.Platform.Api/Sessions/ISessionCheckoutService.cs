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

public sealed record SessionCheckoutQuoteResult(
    bool Succeeded,
    bool NotFound,
    string? Error,
    SessionCheckoutQuoteResponse? Response)
{
    public static SessionCheckoutQuoteResult Ok(SessionCheckoutQuoteResponse response) => new(true, false, null, response);

    public static SessionCheckoutQuoteResult Missing(string error) => new(false, true, error, null);

    public static SessionCheckoutQuoteResult Invalid(string error) => new(false, false, error, null);
}

public interface ISessionCheckoutService
{
    Task<SessionCheckoutResult> CheckoutAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionCheckoutRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Read-only preview of the bill the operator must settle: time charge +
    /// attached unpaid POS = grand total, plus the player's wallet balance for
    /// auto-suggesting a wallet part. Changes nothing.
    /// </summary>
    Task<SessionCheckoutQuoteResult> QuoteAsync(
        Guid sessionId,
        Guid organizationId,
        CancellationToken cancellationToken);
}
