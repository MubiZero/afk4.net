namespace AFK4.Platform.Api.Identity;

public interface IPlayerContextAccessor
{
    PlayerContext? Current { get; set; }
}
