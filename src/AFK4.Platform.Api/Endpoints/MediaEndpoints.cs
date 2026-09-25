using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Media;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Media;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("branches/{branchId:guid}/media", async (
            Guid branchId,
            [FromForm] string purpose,
            IFormFile file,
            StaffAuthorizationService authorizationService,
            IMediaService mediaService,
            CancellationToken ct) =>
        {
            if (!MediaPurposeNames.IsKnown(purpose)) return Results.BadRequest(new { Error = "Unknown media purpose." });
            var auth = await authorizationService.RequireBranchPermissionAsync(branchId, UploadPermissionFor(purpose), ct);
            if (!auth.IsAuthenticated) return Results.Unauthorized();
            if (!auth.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (file is null || file.Length == 0) return Results.BadRequest(new { Error = "File is required." });

            await using var stream = file.OpenReadStream();
            var result = await mediaService.UploadAsync(
                auth.StaffContext!.OrganizationId, branchId, auth.StaffContext.StaffUserId,
                purpose, file.ContentType, stream, file.Length, ct);
            return result.Succeeded
                ? Results.Ok(result.Media)
                : Results.BadRequest(new { Error = result.Error });
        })
            .DisableAntiforgery()
            // Логотип клуба грузят и из мастера установки, не только из панели управляющего.
            .AllowNonOrganizationAdminClients();

        app.MapDelete("branches/{branchId:guid}/media/{mediaId:guid}", async (
            Guid branchId, Guid mediaId,
            StaffAuthorizationService authorizationService,
            IMediaService mediaService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            // Убрать картинку вправе тот же, кто вправе её загрузить. Чужой филиал отсекает проверка
            // права, так что по ответу нельзя узнать, есть ли такой объект у другого клуба.
            var purpose = await db.UploadedMedia.AsNoTracking()
                .Where(media => media.MediaId == mediaId && media.BranchId == branchId)
                .Select(media => media.Purpose)
                .FirstOrDefaultAsync(ct);
            var auth = await authorizationService.RequireBranchPermissionAsync(
                branchId, UploadPermissionFor(purpose ?? string.Empty), ct);
            if (!auth.IsAuthenticated) return Results.Unauthorized();
            if (!auth.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (purpose is null) return Results.NotFound();

            var deleted = await mediaService.DeleteAsync(auth.StaffContext!.OrganizationId, branchId, mediaId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    /// <summary>
    /// Кто вправе загрузить картинку — тот, кто ведёт запись, к которой она относится. Иначе
    /// управляющий бара, которому доверили товары, не мог бы приложить к ним фото.
    /// </summary>
    private static string UploadPermissionFor(string purpose) => purpose switch
    {
        MediaPurposeNames.NewsImage => OrganizationPermissionNames.ManageNews,
        MediaPurposeNames.ProductImage => OrganizationPermissionNames.ManagePosCatalog,
        _ => OrganizationPermissionNames.ManageBranchSettings
    };
}
