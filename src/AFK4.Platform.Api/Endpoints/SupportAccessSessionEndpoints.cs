using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.AspNetCore.RateLimiting;

namespace AFK4.Platform.Api.Endpoints;

internal static class SupportAccessSessionEndpoints
{
    public static void MapSupportAccessSessionEndpoints(this WebApplication app)
    {
        // Публичный: у админки клиента на этом шаге ещё нет ничего, кроме билета.
        app.MapPost("/api/public/support-access/sessions", async (
            RedeemSupportAccessTicketRequest request,
            PlatformSupportAccessGrantService supportAccessService,
            CancellationToken cancellationToken) =>
        {
            var session = await supportAccessService.RedeemTicketAsync(request.Ticket, cancellationToken);
            if (session is null)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(session);
        }).RequireRateLimiting("player-public");

        app.MapDelete("/api/support-access/session", async (
            IPlatformSupportContextAccessor supportContextAccessor,
            PlatformSupportAccessGrantService supportAccessService,
            CancellationToken cancellationToken) =>
        {
            var support = supportContextAccessor.Current;
            if (support is null)
            {
                return Results.Unauthorized();
            }

            await supportAccessService.RevokeAsync(
                support.GrantId, support.PlatformAdminUserId, cancellationToken);
            return Results.NoContent();
        }).AllowPlatformSupportAccess(PlatformSupportSelfPermission);

        app.MapGet("/api/support-access/session", async (
            IPlatformSupportContextAccessor supportContextAccessor,
            PlatformSupportAccessGrantService supportAccessService,
            CancellationToken cancellationToken) =>
        {
            var support = supportContextAccessor.Current;
            if (support is null)
            {
                return Results.Unauthorized();
            }

            // Same PlatformSupportSessionDto shape RedeemTicketAsync returns (branches included) —
            // a tab reload under an active support session needs to land back in a working shell,
            // not a shape the client doesn't know how to render.
            return Results.Ok(await supportAccessService.DescribeSessionAsync(support, cancellationToken));
        }).AllowPlatformSupportAccess(PlatformSupportSelfPermission);
    }

    // Собственные эндпоинты сессии не требуют прав клуба: это управление самой сессией.
    private const string PlatformSupportSelfPermission = "organization.support_access.self";
}
