namespace AFK4.Platform.Api.Identity;

public sealed record StaffAuthorizationResult(
    bool IsAllowed,
    bool IsAuthenticated,
    StaffContext? StaffContext,
    string? DenialReason)
{
    public static StaffAuthorizationResult Unauthenticated()
    {
        return new StaffAuthorizationResult(false, false, null, "Authentication is required.");
    }

    public static StaffAuthorizationResult Denied(StaffContext context, string reason)
    {
        return new StaffAuthorizationResult(false, true, context, reason);
    }

    public static StaffAuthorizationResult Allowed(StaffContext context)
    {
        return new StaffAuthorizationResult(true, true, context, null);
    }
}
