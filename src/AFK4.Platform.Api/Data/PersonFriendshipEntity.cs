namespace AFK4.Platform.Api.Data;

/// <summary>
/// Дружба двух людей — не двух клубных карточек: друг остаётся другом в любом клубе сети, как
/// и сама личность (см. <see cref="PlatformPersonEntity"/>).
///
/// Строка одна на пару и хранит, кто позвал: это и заявка, и дружба. Отклонённая заявка тоже
/// остаётся строкой — по ней видно, что человек уже отказал, и второй раз его не зовут.
/// </summary>
public sealed class PersonFriendshipEntity
{
    public Guid PersonFriendshipId { get; set; }

    public Guid RequesterPersonId { get; set; }

    public Guid AddresseePersonId { get; set; }

    /// <see cref="AFK4.Shared.Contracts.Friends.FriendshipStateNames"/>.
    public string State { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RespondedAtUtc { get; set; }
}
