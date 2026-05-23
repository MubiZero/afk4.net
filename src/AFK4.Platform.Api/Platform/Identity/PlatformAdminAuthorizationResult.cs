namespace AFK4.Platform.Api.Platform.Identity;

public sealed record PlatformAdminAuthorizationResult(
    bool IsAuthenticated,
    bool IsAllowed,
    PlatformAdminContext? PlatformAdminContext,
    string? DenialReason)
{
    public static PlatformAdminAuthorizationResult Unauthenticated()
    {
        return new PlatformAdminAuthorizationResult(false, false, null, "No authenticated platform admin context.");
    }

    public static PlatformAdminAuthorizationResult Denied(PlatformAdminContext context, string reason)
    {
        return new PlatformAdminAuthorizationResult(true, false, context, reason);
    }

    public static PlatformAdminAuthorizationResult Allowed(PlatformAdminContext context)
    {
        return new PlatformAdminAuthorizationResult(true, true, context, null);
    }
}
