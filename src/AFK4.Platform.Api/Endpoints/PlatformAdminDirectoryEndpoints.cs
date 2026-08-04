using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Platform.Auth;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformAdminDirectoryEndpoints
{
    public static void MapPlatformAdminDirectoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/admins", async (
            PlatformAdminAuthorizationService authorizationService,
            PlatformAdminDirectoryService directoryService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.ViewPlatformAdmins,
                    targetType: "PlatformAdminUser",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var items = await directoryService.ListAsync(cancellationToken);

            // Details deliberately hold only a count — the list itself carries roles, activity and
            // 2FA status per admin, and none of that belongs in the audit trail's details payload.
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                action: AuditActionNames.ViewPlatformAdmins,
                targetType: "PlatformAdminUser",
                targetId: null,
                outcome: AuditOutcome.Succeeded,
                details: new { Count = items.Count },
                cancellationToken);

            return Results.Ok(items);
        });

        app.MapPost("/api/platform/admins/invitations", async (
            CreatePlatformAdminInvitationRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAdminDirectoryService directoryService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.PlatformAdminInvited,
                    targetType: "PlatformAdminInvitation",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.Role, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var actorId = authorization.PlatformAdminContext!.PlatformAdminUserId;
            var (response, error) = await directoryService.InviteAsync(actorId, request, cancellationToken);

            if (error != PlatformAdminDirectoryError.None)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: actorId,
                    action: AuditActionNames.PlatformAdminInvited,
                    targetType: "PlatformAdminInvitation",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.Role, Error = error.ToString() },
                    cancellationToken);
                return DirectoryErrorResult(error);
            }

            // The invitation code is returned to the caller once and must never be persisted into
            // the audit trail (log/details), so the audit details deliberately omit response.Code.
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: actorId,
                action: AuditActionNames.PlatformAdminInvited,
                targetType: "PlatformAdminInvitation",
                targetId: response!.Invitation.InvitationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { response.Invitation.Role, response.Invitation.ExpiresAtUtc },
                cancellationToken);

            return Results.Ok(response);
        });

        app.MapPost("/api/platform/admins/invitations/{invitationId:guid}/revoke", async (
            Guid invitationId,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAdminDirectoryService directoryService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.PlatformAdminInvitationRevoked,
                    targetType: "PlatformAdminInvitation",
                    targetId: invitationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var actorId = authorization.PlatformAdminContext!.PlatformAdminUserId;
            var error = await directoryService.RevokeInvitationAsync(invitationId, cancellationToken);

            if (error != PlatformAdminDirectoryError.None)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: actorId,
                    action: AuditActionNames.PlatformAdminInvitationRevoked,
                    targetType: "PlatformAdminInvitation",
                    targetId: invitationId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { Error = error.ToString() },
                    cancellationToken);
                return DirectoryErrorResult(error);
            }

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: actorId,
                action: AuditActionNames.PlatformAdminInvitationRevoked,
                targetType: "PlatformAdminInvitation",
                targetId: invitationId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { },
                cancellationToken);

            return Results.Ok();
        });

        app.MapPatch("/api/platform/admins/{platformAdminUserId:guid}", async (
            Guid platformAdminUserId,
            UpdatePlatformAdminRequest request,
            PlatformAdminAuthorizationService authorizationService,
            PlatformAdminDirectoryService directoryService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlatformAdmins);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.PlatformAdminUpdated,
                    targetType: "PlatformAdminUser",
                    targetId: platformAdminUserId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { request.Role, request.IsActive, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var actorId = authorization.PlatformAdminContext!.PlatformAdminUserId;
            var (item, error) = await directoryService.UpdateAsync(actorId, platformAdminUserId, request, cancellationToken);

            if (error != PlatformAdminDirectoryError.None)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId: Guid.Empty,
                    actorPlatformAdminUserId: actorId,
                    action: AuditActionNames.PlatformAdminUpdated,
                    targetType: "PlatformAdminUser",
                    targetId: platformAdminUserId.ToString("D"),
                    outcome: AuditOutcome.Denied,
                    details: new { request.Role, request.IsActive, Error = error.ToString() },
                    cancellationToken);
                return DirectoryErrorResult(error);
            }

            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId: Guid.Empty,
                actorPlatformAdminUserId: actorId,
                action: AuditActionNames.PlatformAdminUpdated,
                targetType: "PlatformAdminUser",
                targetId: platformAdminUserId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { item!.Role, item.IsActive },
                cancellationToken);

            return Results.Ok(item);
        });
    }

    // Maps a PlatformAdminDirectoryError to an HTTP result. Conflict is a generic "a concurrent
    // change won the race, retry" outcome (see PlatformAdminDirectoryService) and must carry a
    // message distinct from LastFullAdmin — otherwise a caller who lost a race gets a false
    // explanation ("you'd leave the platform without admins") instead of the true one ("retry").
    private static IResult DirectoryErrorResult(PlatformAdminDirectoryError error) => error switch
    {
        PlatformAdminDirectoryError.NotFound => Results.NotFound(new
        {
            Error = "not_found",
            Message = "Platform admin or invitation was not found."
        }),
        PlatformAdminDirectoryError.UnknownRole => Results.BadRequest(new
        {
            Error = "unknown_role",
            Message = "Requested role is not recognized."
        }),
        PlatformAdminDirectoryError.LastFullAdmin => Results.Conflict(new
        {
            Error = "last_full_admin",
            Message = "At least one active platform administrator must remain."
        }),
        PlatformAdminDirectoryError.SelfDemotion => Results.Conflict(new
        {
            Error = "self_demotion",
            Message = "You cannot demote or deactivate your own platform admin account."
        }),
        PlatformAdminDirectoryError.Conflict => Results.Conflict(new
        {
            Error = "conflict",
            Message = "A concurrent change affected this record. Please retry the action."
        }),
        _ => Results.Problem("Unexpected platform admin directory error.", statusCode: StatusCodes.Status500InternalServerError)
    };
}
