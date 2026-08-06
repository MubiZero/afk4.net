namespace AFK4.Platform.Api.Platform.Support;

public interface IPlatformSupportContextAccessor
{
    PlatformSupportContext? Current { get; set; }
}

public sealed class PlatformSupportContextAccessor : IPlatformSupportContextAccessor
{
    public PlatformSupportContext? Current { get; set; }
}
