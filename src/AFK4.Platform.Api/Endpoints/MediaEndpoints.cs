using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Media;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AFK4.Platform.Api.Endpoints;

internal static class MediaEndpoints
{
    public static void MapMediaEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/media", async (
            Guid branchId,
            [FromForm] string purpose,
            IFormFile file,
            StaffAuthorizationService authorizationService,
            IMediaService mediaService,
            CancellationToken ct) =>
        {
            var auth = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageBranchSettings, ct);
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
        }).DisableAntiforgery();

        app.MapDelete("/api/branches/{branchId:guid}/media/{mediaId:guid}", async (
            Guid branchId, Guid mediaId,
            StaffAuthorizationService authorizationService,
            IMediaService mediaService,
            CancellationToken ct) =>
        {
            var auth = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageBranchSettings, ct);
            if (!auth.IsAuthenticated) return Results.Unauthorized();
            if (!auth.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var deleted = await mediaService.DeleteAsync(auth.StaffContext!.OrganizationId, branchId, mediaId, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }
}
