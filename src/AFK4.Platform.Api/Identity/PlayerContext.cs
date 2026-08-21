namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Игрок в конкретном клубе: чей это счёт и чьи это деньги. <see cref="PlatformPersonId"/> —
/// человек, которому счёт принадлежит; null у счетов, которые к личности ещё не подшиты (гость,
/// заведённый на стойке без телефона), и у входов по старым токенам.
/// </summary>
public sealed record PlayerContext(
    Guid PlayerAccountId,
    Guid OrganizationId,
    bool PhoneVerified,
    Guid? PlatformPersonId = null);
