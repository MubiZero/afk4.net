namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Кто пришёл, независимо от того, в каком клубе он сейчас. Живёт рядом с
/// <see cref="PlayerContext"/>, а не вместо него: клубный контекст отвечает на вопрос «чей это
/// счёт и чьи это деньги», а этот — на вопрос «кто этот человек».
/// </summary>
/// <param name="SelectedOrganizationId">
/// Клуб, о котором идёт этот запрос, — независимо от того, есть ли у человека в нём счёт. Именно
/// сюда открывается счёт, если запрос оказался первым действием.
/// </param>
public sealed record PlatformPersonContext(
    Guid PlatformPersonId,
    Guid? PinnedOrganizationId,
    bool PhoneVerified,
    Guid? SelectedOrganizationId = null);

public interface IPlatformPersonContextAccessor
{
    PlatformPersonContext? Current { get; set; }
}

public sealed class PlatformPersonContextAccessor : IPlatformPersonContextAccessor
{
    public PlatformPersonContext? Current { get; set; }
}
