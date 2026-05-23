namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PlatformAdminContextAccessor : IPlatformAdminContextAccessor
{
    public PlatformAdminContext? Current { get; set; }
}
