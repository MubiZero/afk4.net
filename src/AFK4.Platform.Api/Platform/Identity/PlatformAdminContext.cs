using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed record PlatformAdminContext(
    Guid PlatformAdminUserId,
    string UserName,
    string DisplayName,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions)
{
    public AuthenticationDomain Domain => AuthenticationDomain.Platform;
}
