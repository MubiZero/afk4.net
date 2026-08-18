namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Кто пришёл, независимо от того, в каком клубе он сейчас. Живёт рядом с
/// <see cref="PlayerContext"/>, а не вместо него: клубный контекст отвечает на вопрос «чей это
/// счёт и чьи это деньги», а этот — на вопрос «кто этот человек».
/// </summary>
public sealed record PlatformPersonContext(
    Guid PlatformPersonId,
    Guid? PinnedOrganizationId,
    bool PhoneVerified);

public interface IPlatformPersonContextAccessor
{
    PlatformPersonContext? Current { get; set; }
}

public sealed class PlatformPersonContextAccessor : IPlatformPersonContextAccessor
{
    public PlatformPersonContext? Current { get; set; }
}
