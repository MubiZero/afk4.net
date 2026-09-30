using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

public sealed record SessionCheckoutResult(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    SessionCheckoutResponse? Response,
    string? Code = null,
    int? CurrentVersion = null)
{
    public static SessionCheckoutResult Ok(SessionCheckoutResponse response) => new(true, false, false, null, response);

    public static SessionCheckoutResult RequestConflict(string error, string? code = null) => new(false, true, false, error, null, code);

    public static SessionCheckoutResult StaleVersion(int currentVersion) =>
        new(false, true, false, "This session changed since you last loaded it; refresh and try again.", null, "stale_version", currentVersion);

    public static SessionCheckoutResult Missing(string error) => new(false, false, true, error, null);

    public static SessionCheckoutResult Invalid(string error, string? code = null) =>
        new(false, false, false, error, null, MachineErrorCode.Resolve(error, code));
}

public sealed record SessionCheckoutQuoteResult(
    bool Succeeded,
    bool NotFound,
    string? Error,
    SessionCheckoutQuoteResponse? Response,
    string? Code = null)
{
    public static SessionCheckoutQuoteResult Ok(SessionCheckoutQuoteResponse response) => new(true, false, null, response);

    public static SessionCheckoutQuoteResult Missing(string error) => new(false, true, error, null);

    public static SessionCheckoutQuoteResult Invalid(string error, string? code = null) =>
        new(false, false, error, null, MachineErrorCode.Resolve(error, code));
}
