using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Organizations;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

public static class PlatformBranchEndpoints
{
    public static void MapPlatformBranchEndpoints(this WebApplication app)
    {
        app.MapPost("/api/platform/organizations/{organizationId:guid}/branches", async (
            Guid organizationId,
            CreateBranchRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformOrganizationService organizationService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.CreateOrganization);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
                    action: AuditActionNames.CreateBranch,
                    targetType: "Branch",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.Slug, authorization.DenialReason },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await organizationService.CreateBranchAsync(
                organizationId,
                request,
                authorization.PlatformAdminContext!.PlatformAdminUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WritePlatformAuditAsync(
                    auditRecordWriter,
                    organizationId,
                    actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                    action: AuditActionNames.CreateBranch,
                    targetType: "Branch",
                    targetId: null,
                    outcome: AuditOutcome.Denied,
                    details: new { request.Slug, Error = result.Error },
                    cancellationToken);

                return result.Status switch
                {
                    PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                    PlatformOrganizationOperationStatus.PlanLimitReached =>
                        Results.Conflict(new { Error = result.Error, result.PlanLimit!.Code, PlanLimit = result.PlanLimit }),
                    PlatformOrganizationOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                    _ => Results.BadRequest(new { Error = result.Error })
                };
            }

            var branch = result.Value!;
            await WritePlatformAuditAsync(
                auditRecordWriter,
                organizationId,
                actorPlatformAdminUserId: authorization.PlatformAdminContext.PlatformAdminUserId,
                action: AuditActionNames.CreateBranch,
                targetType: "Branch",
                targetId: branch.BranchId.ToString("D"),
                outcome: AuditOutcome.Succeeded,
                details: new { branch.Slug, branch.Name, branch.City },
                cancellationToken);

            return Results.Ok(branch);
        });
    }
}
