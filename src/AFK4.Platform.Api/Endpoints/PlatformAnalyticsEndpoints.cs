using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformAnalyticsEndpoints
{
    public static void MapPlatformAnalyticsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/analytics/overview", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformAnalyticsService analyticsService,
            int? months,
            CancellationToken cancellationToken) =>
        {
            // Право проверяется ДО обращения к данным.
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await analyticsService.GetOverviewAsync(months ?? 12, cancellationToken));
        });

        // Единственное число, по которому виден конец перехода на сетевой PIN. Лежит отдельным
        // маршрутом, а не полем в сводке: обзорную сводку читает Platform Control, и подмешивать
        // в контракт про деньги показатель про переход значит просить чужой экран пережить
        // чужое изменение. Уходит вместе с переходом.
        app.MapGet("/api/platform/analytics/pin-adoption", async (
            PlatformAdminAuthorizationService authorizationService,
            PinAdoptionReader pinAdoption,
            int? windowDays,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewPlatformHealth);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await pinAdoption.ReadAsync(
                windowDays ?? PinAdoptionReader.DefaultWindowDays, cancellationToken));
        });
    }
}
