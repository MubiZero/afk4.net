namespace AFK4.Shared.Contracts.Friends;

/// <summary>
/// Друг и то, единственное, что о нём видно: имя и «сейчас в зале» — если он сам это показывает.
/// Ни телефона, ни денег, ни истории: дружба не даёт доступа к чужому счёту.
/// </summary>
public sealed record FriendDto(
    Guid PlatformPersonId,
    string DisplayName,
    /// Где он сейчас играет. null — не в зале, или он скрыл своё присутствие. Разницы снаружи
    /// нет намеренно: иначе «скрыт» читалось бы как «он там, но прячется».
    FriendPresenceDto? Presence);

/// <summary>Клуб и зал, в которых друг сейчас за ПК.</summary>
public sealed record FriendPresenceDto(string OrganizationName, string BranchName);

/// <summary>
/// Заявка в друзья. Пришедшую можно принять или отклонить, отправленную — только ждать:
/// отзывать её незачем, а кнопка «отозвать» превратила бы список в пульт.
/// </summary>
public sealed record FriendRequestDto(
    Guid FriendRequestId,
    Guid PlatformPersonId,
    string DisplayName,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Друзья целиком: принятые, пришедшие заявки и отправленные. Один ответ на весь экран —
/// три запроса ради трёх списков платили бы сетью за одно открытие.
/// </summary>
public sealed record FriendsDto(
    IReadOnlyList<FriendDto> Friends,
    IReadOnlyList<FriendRequestDto> Incoming,
    IReadOnlyList<FriendRequestDto> Outgoing,
    /// Видят ли друзья, что человек сейчас в зале. Выключено — список друзей у него остаётся,
    /// но его самого в залах никто не видит.
    bool ShowsPresence);

/// <summary>Позвать в друзья по номеру — тому, который человек и так знает.</summary>
public sealed record SendFriendRequestRequest(string PhoneNumber);

/// <summary>Показывать ли друзьям, что я сейчас в зале.</summary>
public sealed record UpdatePresenceVisibilityRequest(bool ShowsPresence);

/// <summary>
/// Коды отказов. Ответ на заявку по чужому номеру намеренно одинаков всегда — по нему нельзя
/// узнать, зарегистрирован ли номер в сети.
/// </summary>
public static class FriendRefusalCodes
{
    public const string Self = "friend_self";
    public const string AlreadyFriends = "friend_already";
    public const string RequestExists = "friend_request_exists";
    public const string NoSuchRequest = "friend_request_unknown";
}

/// <summary>Что с дружбой прямо сейчас.</summary>
public static class FriendshipStateNames
{
    /// Позвали, ответа ещё нет.
    public const string Pending = "pending";

    public const string Accepted = "accepted";

    /// Отказали. Строка остаётся, чтобы человека не звали второй раз.
    public const string Declined = "declined";
}
