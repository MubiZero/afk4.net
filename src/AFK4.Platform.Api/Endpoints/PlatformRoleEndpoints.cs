using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Управление ролями платформы. Отдельного права не заводится: кто управляет администраторами,
/// тот управляет и ролями — лишнее право усложнило бы модель, ничего не добавив.
/// </summary>
public static class PlatformRoleEndpoints
{
    public static void MapPlatformRoleEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/permissions", (PlatformAdminAuthorizationService authorizationService) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(PlatformAdminPermissionNames.All);
        });

        app.MapGet("/api/platform/roles", async (
            PlatformAdminAuthorizationService authorizationService,
            PlatformRoleService roleService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteRoleAuditAsync(auditRecordWriter, authorization, AuditActionNames.ViewPlatformRoles,
                    targetId: null, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await roleService.ListAsync(cancellationToken));
        });

        app.MapPost("/api/platform/roles", async (
            CreatePlatformRoleRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformRoleService roleService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteRoleAuditAsync(auditRecordWriter, authorization, AuditActionNames.CreatePlatformRole,
                    request.RoleName, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await roleService.CreateAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, request, cancellationToken);

            if (result.Succeeded)
            {
                await WriteRoleAuditAsync(auditRecordWriter, authorization, AuditActionNames.CreatePlatformRole,
                    result.Value!.RoleName, AuditOutcome.Succeeded,
                    new { result.Value.Permissions }, cancellationToken);
            }

            return MapResult(result);
        });

        app.MapPut("/api/platform/roles/{roleName}", async (
            string roleName,
            UpdatePlatformRoleRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformRoleService roleService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteRoleAuditAsync(auditRecordWriter, authorization, AuditActionNames.UpdatePlatformRole,
                    roleName, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await roleService.UpdateAsync(
                authorization.PlatformAdminContext!.PlatformAdminUserId, roleName, request, cancellationToken);

            if (result.Succeeded)
            {
                await WriteRoleAuditAsync(auditRecordWriter, authorization, AuditActionNames.UpdatePlatformRole,
                    roleName, AuditOutcome.Succeeded, new { result.Value!.Permissions }, cancellationToken);
            }

            return MapResult(result);
        });

        app.MapDelete("/api/platform/roles/{roleName}", async (
            string roleName,
            PlatformAdminAuthorizationService authorizationService,
            PlatformRoleService roleService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteRoleAuditAsync(auditRecordWriter, authorization, AuditActionNames.DeletePlatformRole,
                    roleName, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await roleService.DeleteAsync(roleName, cancellationToken);

            if (result.Succeeded)
            {
                await WriteRoleAuditAsync(auditRecordWriter, authorization, AuditActionNames.DeletePlatformRole,
                    roleName, AuditOutcome.Succeeded, new { }, cancellationToken);
            }

            return MapResult(result);
        });
    }

    private static IResult MapResult(PlatformRoleResult result) =>
        result.ErrorCode switch
        {
            null => Results.Ok(result.Value),
            PlatformRoleErrorCodes.InvalidRole => Results.BadRequest(new { Code = result.ErrorCode }),
            PlatformRoleErrorCodes.UnknownPermission => Results.BadRequest(new { Code = result.ErrorCode }),
            PlatformRoleErrorCodes.PermissionNotHeldByActor =>
                Results.Json(new { Code = result.ErrorCode }, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Conflict(new { Code = result.ErrorCode })
        };

    private static Task WriteRoleAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        PlatformAdminAuthorizationResult authorization,
        string action,
        string? targetId,
        string outcome,
        object details,
        CancellationToken cancellationToken) =>
        WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext?.PlatformAdminUserId,
            action: action,
            targetType: "PlatformRole",
            targetId: targetId,
            outcome: outcome,
            details: details,
            cancellationToken);
}
