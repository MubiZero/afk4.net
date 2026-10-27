using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications.Telegram;
using AFK4.Shared.Contracts.Identity;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// «Отчёты в Telegram» для сотрудника, который сам их получает. Право — смотреть отчёты: подключить
/// свой чат к сводкам выручки должен уметь только тот, кто видит их в Панели.
/// </summary>
internal static class StaffTelegramEndpoints
{
    public static void MapStaffTelegramEndpoints(this IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("account/telegram", async (
            StaffAuthorizationService authorizationService, TelegramLinks links, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewReports);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await links.GetStatusAsync(authorization.StaffContext!.StaffUserId, ct));
        });

        organizations.MapPost("account/telegram/link", async (
            StaffAuthorizationService authorizationService, TelegramLinks links, IOptions<TelegramOptions> options, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewReports);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!options.Value.IsConfigured)
            {
                return Results.Json(
                    new { error = "Telegram bot is not configured.", code = StaffTelegramErrorCodeNames.NotConfigured },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                return Results.Ok(await links.CreateLinkAsync(authorization.StaffContext!.StaffUserId, ct));
            }
            catch (TelegramApiException)
            {
                return Results.Json(
                    new { error = "Telegram is unreachable.", code = StaffTelegramErrorCodeNames.Unreachable },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        organizations.MapDelete("account/telegram", async (
            StaffAuthorizationService authorizationService, TelegramLinks links, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewReports);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            await links.UnlinkAsync(authorization.StaffContext!.StaffUserId, ct);
            return Results.NoContent();
        });
    }
}
