using AFK4.Platform.Api.Media;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Media;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Картинки, которые грузит сама платформа: обложки каталога игр и картинки рекламы — в то же
/// хранилище (MinIO), что и картинки клубов, без ссылок на чужие сайты.
/// </summary>
internal static class PlatformMediaEndpoints
{
    public static void MapPlatformMediaEndpoints(this WebApplication app)
    {
        app.MapPost(PlatformMediaRoutes.Upload, async (
            [FromForm] string purpose, IFormFile file, PlatformAdminAuthorizationService authorizationService,
            IMediaStorage storage, IOptions<MediaOptions> options, CancellationToken ct) =>
        {
            if (!PlatformMedia.IsKnown(purpose)) return Results.BadRequest(new { Error = PlatformMediaErrorCodeNames.UnknownPurpose });
            var authorization = authorizationService.RequirePermission(purpose == PlatformMediaPurposeNames.AdCreative
                ? PlatformAdminPermissionNames.ManageAds
                : PlatformAdminPermissionNames.ManageGameCatalog);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (file is null || file.Length == 0) return Results.BadRequest(new { Error = PlatformMediaErrorCodeNames.FileRequired });

            await using var stream = file.OpenReadStream();
            var (url, error) = await PlatformMedia.PutAsync(storage, options.Value, purpose, stream, file.Length, ct);
            return url is null ? Results.BadRequest(new { Error = error }) : Results.Ok(new PlatformMediaUploadedDto(url));
        }).DisableAntiforgery();

        // «Подтянуть из Steam»: картинка магазина по номеру приложения, копией в нашем хранилище.
        app.MapPost(PlatformMediaRoutes.SteamCover, async (
            SteamCoverRequest request, PlatformAdminAuthorizationService authorizationService, ISteamCoverSource source,
            IMediaStorage storage, IOptions<MediaOptions> options, CancellationToken ct) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageGameCatalog);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var appId = request.SteamAppId?.Trim() ?? string.Empty;
            if (appId.Length is 0 or > 10 || !appId.All(char.IsAsciiDigit)) return Results.BadRequest(new { Error = PlatformMediaErrorCodeNames.SteamAppIdInvalid });

            var url = await SteamCovers.StoreAsync(source, storage, options.Value, appId, ct);
            return url is null ? Results.NotFound(new { Error = PlatformMediaErrorCodeNames.SteamCoverNotFound }) : Results.Ok(new PlatformMediaUploadedDto(url));
        });
    }
}
