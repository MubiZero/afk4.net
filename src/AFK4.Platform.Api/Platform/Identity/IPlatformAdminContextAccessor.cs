namespace AFK4.Platform.Api.Platform.Identity;

public interface IPlatformAdminContextAccessor
{
    PlatformAdminContext? Current { get; set; }
}
