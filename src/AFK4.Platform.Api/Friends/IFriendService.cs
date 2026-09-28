using AFK4.Shared.Contracts.Friends;

namespace AFK4.Platform.Api.Friends;

/// <param name="Error">Код из <see cref="FriendRefusalCodes"/> — фразу собирает клиент.</param>
public sealed record FriendActionResult(bool Succeeded, string? Error)
{
    public static FriendActionResult Ok() => new(true, null);

    public static FriendActionResult Refused(string error) => new(false, error);
}
