using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Updates;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformUpdateEndpoints
{
    public static void MapPlatformUpdateEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/updates/packages", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformUpdateReleaseService releaseService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewUpdates);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await releaseService.ListPackagesAsync(cancellationToken));
        });

        app.MapPost("/api/platform/updates/packages", async (
            CreatePlatformUpdatePackageRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformUpdateReleaseService releaseService,
            IAuditRecordWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageUpdatePackages);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await AuditAsync(auditWriter, authorization.PlatformAdminContext!.PlatformAdminUserId,
                    AuditActionNames.PlatformRegisterUpdatePackage, "UpdatePackage", null,
                    AuditOutcome.Denied, new { request.Component, request.Version, authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await releaseService.RegisterPackageAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, request, cancellationToken);
            await AuditResultAsync(auditWriter, authorization.PlatformAdminContext.PlatformAdminUserId,
                AuditActionNames.PlatformRegisterUpdatePackage, "UpdatePackage",
                result.Response?.UpdatePackageId.ToString("D"), result, cancellationToken);
            return ToHttpResult(result);
        });

        app.MapPost("/api/platform/updates/packages/{packageId:guid}/state", async (
            Guid packageId,
            ChangePlatformUpdatePackageStateRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformUpdateReleaseService releaseService,
            IAuditRecordWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageUpdatePackages);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var result = await releaseService.ChangePackageStateAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, packageId, request, cancellationToken);
            await AuditResultAsync(auditWriter, authorization.PlatformAdminContext.PlatformAdminUserId,
                AuditActionNames.PlatformChangeUpdatePackageState, "UpdatePackage", packageId.ToString("D"), result, cancellationToken);
            return ToHttpResult(result);
        });

        app.MapGet("/api/platform/updates/rollouts", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformUpdateReleaseService releaseService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewUpdates);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await releaseService.ListRolloutsAsync(cancellationToken));
        });

        app.MapPost("/api/platform/updates/rollouts", async (
            CreatePlatformUpdateRolloutRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformUpdateReleaseService releaseService,
            IAuditRecordWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageUpdateRollouts);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var result = await releaseService.CreateRolloutAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, request, cancellationToken);
            await AuditResultAsync(auditWriter, authorization.PlatformAdminContext.PlatformAdminUserId,
                AuditActionNames.PlatformCreateUpdateRollout, "UpdateRollout",
                result.Response?.UpdateRolloutId.ToString("D"), result, cancellationToken);
            return ToHttpResult(result);
        });

        app.MapPost("/api/platform/updates/rollouts/{rolloutId:guid}/state", async (
            Guid rolloutId,
            ChangePlatformUpdateRolloutStateRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformUpdateReleaseService releaseService,
            IAuditRecordWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageUpdateRollouts);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var result = await releaseService.ChangeRolloutStateAsync(rolloutId, request, cancellationToken);
            await AuditResultAsync(auditWriter, authorization.PlatformAdminContext!.PlatformAdminUserId,
                AuditActionNames.PlatformChangeUpdateRolloutState, "UpdateRollout", rolloutId.ToString("D"), result, cancellationToken);
            return ToHttpResult(result);
        });
    }

    private static IResult ToHttpResult<T>(UpdateServiceResult<T> result)
    {
        if (result.Succeeded) return Results.Ok(result.Response);
        if (result.Conflict) return Results.Conflict(new { result.Error });
        if (result.NotFound) return Results.NotFound(new { result.Error });
        return Results.BadRequest(new { result.Error });
    }

    private static Task AuditResultAsync<T>(
        IAuditRecordWriter writer,
        Guid actorId,
        string action,
        string targetType,
        string? targetId,
        UpdateServiceResult<T> result,
        CancellationToken cancellationToken) => AuditAsync(
            writer, actorId, action, targetType, targetId,
            result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
            new { result.Error }, cancellationToken);

    private static Task AuditAsync(
        IAuditRecordWriter writer,
        Guid actorId,
        string action,
        string targetType,
        string? targetId,
        string outcome,
        object details,
        CancellationToken cancellationToken) => writer.WriteAsync(new AuditRecordWriteRequest(
            Guid.Empty, null, null, action, targetType, targetId, outcome, "PlatformApi", JsonSerializer.Serialize(details))
        {
            ActorPlatformAdminUserId = actorId
        }, cancellationToken);
}
