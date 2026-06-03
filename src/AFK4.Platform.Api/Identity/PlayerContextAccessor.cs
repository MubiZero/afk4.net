namespace AFK4.Platform.Api.Identity;

public sealed class PlayerContextAccessor : IPlayerContextAccessor
{
    public PlayerContext? Current { get; set; }
}
