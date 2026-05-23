namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PlatformAdminAuthorizationService(IPlatformAdminContextAccessor contextAccessor)
{
    public PlatformAdminAuthorizationResult RequirePermission(string permission)
    {
        var context = contextAccessor.Current;

        if (context is null)
        {
            return PlatformAdminAuthorizationResult.Unauthenticated();
        }

        if (!context.Permissions.Contains(permission))
        {
            return PlatformAdminAuthorizationResult.Denied(
                context,
                "Platform admin does not have the required permission.");
        }

        return PlatformAdminAuthorizationResult.Allowed(context);
    }

    public PlatformAdminAuthorizationResult RequireAuthenticated()
    {
        var context = contextAccessor.Current;
        return context is null
            ? PlatformAdminAuthorizationResult.Unauthenticated()
            : PlatformAdminAuthorizationResult.Allowed(context);
    }
}
