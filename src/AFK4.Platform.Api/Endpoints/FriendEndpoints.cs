using AFK4.Platform.Api.Friends;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Friends;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Друзья человека. Маршруты личности, а не клуба: друг остаётся другом в любом клубе сети,
/// поэтому заголовок клуба здесь ни на что не влияет.
/// </summary>
internal static class FriendEndpoints
{
    public static void MapFriendEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/friends", async (
            IPlatformPersonContextAccessor personContextAccessor,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var person = personContextAccessor.Current;
            if (person is null) return Results.Unauthorized();

            return Results.Ok(await friends.ListAsync(person.PlatformPersonId, ct));
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/friends/requests", async (
            SendFriendRequestRequest request,
            IPlatformPersonContextAccessor personContextAccessor,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var person = personContextAccessor.Current;
            if (person is null) return Results.Unauthorized();

            var result = await friends.RequestAsync(person.PlatformPersonId, request.PhoneNumber, ct);
            // Ответ одинаков для любого чужого номера — по нему нельзя узнать, есть ли этот
            // номер в сети (см. EfFriendService.RequestAsync).
            return result.Succeeded
                ? Results.Ok(await friends.ListAsync(person.PlatformPersonId, ct))
                : Results.Conflict(new { error = result.Error });
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/friends/requests/{friendRequestId:guid}/accept", async (
            Guid friendRequestId,
            IPlatformPersonContextAccessor personContextAccessor,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var person = personContextAccessor.Current;
            if (person is null) return Results.Unauthorized();

            var result = await friends.AcceptAsync(person.PlatformPersonId, friendRequestId, ct);
            return result.Succeeded
                ? Results.Ok(await friends.ListAsync(person.PlatformPersonId, ct))
                : Results.NotFound(new { error = result.Error });
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/friends/requests/{friendRequestId:guid}/decline", async (
            Guid friendRequestId,
            IPlatformPersonContextAccessor personContextAccessor,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var person = personContextAccessor.Current;
            if (person is null) return Results.Unauthorized();

            var result = await friends.DeclineAsync(person.PlatformPersonId, friendRequestId, ct);
            return result.Succeeded
                ? Results.Ok(await friends.ListAsync(person.PlatformPersonId, ct))
                : Results.NotFound(new { error = result.Error });
        }).RequireRateLimiting("player-me");

        app.MapDelete("/api/me/friends/{friendPersonId:guid}", async (
            Guid friendPersonId,
            IPlatformPersonContextAccessor personContextAccessor,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var person = personContextAccessor.Current;
            if (person is null) return Results.Unauthorized();

            var result = await friends.RemoveAsync(person.PlatformPersonId, friendPersonId, ct);
            return result.Succeeded
                ? Results.Ok(await friends.ListAsync(person.PlatformPersonId, ct))
                : Results.NotFound(new { error = result.Error });
        }).RequireRateLimiting("player-me");

        // Один переключатель на всех друзей сразу. Выборочная видимость («этому показываю,
        // этому нет») — это уже настройка, которую надо помнить и поддерживать; человеку хватает
        // одного ответа на вопрос «видно ли меня».
        app.MapPatch("/api/me/friends/presence", async (
            UpdatePresenceVisibilityRequest request,
            IPlatformPersonContextAccessor personContextAccessor,
            IFriendService friends,
            CancellationToken ct) =>
        {
            var person = personContextAccessor.Current;
            if (person is null) return Results.Unauthorized();

            var result = await friends.SetPresenceVisibilityAsync(
                person.PlatformPersonId, request.ShowsPresence, ct);
            return result.Succeeded
                ? Results.Ok(await friends.ListAsync(person.PlatformPersonId, ct))
                : Results.NotFound(new { error = result.Error });
        }).RequireRateLimiting("player-me");
    }
}
