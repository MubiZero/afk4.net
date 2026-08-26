using AFK4.Shared.Contracts.Friends;

namespace AFK4.Platform.Api.Friends;

/// <summary>
/// Друзья человека и его присутствие в зале. Всё — на уровне личности: клуб здесь ни при чём,
/// друг остаётся другом в любом клубе сети.
/// </summary>
public interface IFriendService
{
    Task<FriendsDto> ListAsync(Guid personId, CancellationToken ct);

    Task<FriendActionResult> RequestAsync(Guid personId, string phoneNumber, CancellationToken ct);

    Task<FriendActionResult> AcceptAsync(Guid personId, Guid friendRequestId, CancellationToken ct);

    Task<FriendActionResult> DeclineAsync(Guid personId, Guid friendRequestId, CancellationToken ct);

    Task<FriendActionResult> RemoveAsync(Guid personId, Guid friendPersonId, CancellationToken ct);

    Task<FriendActionResult> SetPresenceVisibilityAsync(Guid personId, bool showsPresence, CancellationToken ct);
}

/// <param name="Error">Код из <see cref="FriendRefusalCodes"/> — фразу собирает клиент.</param>
public sealed record FriendActionResult(bool Succeeded, string? Error)
{
    public static FriendActionResult Ok() => new(true, null);

    public static FriendActionResult Refused(string error) => new(false, error);
}
