using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class UpdateEndpoints
{
    public static void MapUpdateEndpoints(this WebApplication app, IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("branches/{branchId:guid}/updates/preferences", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewUpdateStatus, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var branch = await dbContext.Branches.AsNoTracking().SingleAsync(
                candidate => candidate.BranchId == branchId && candidate.OrganizationId == authorization.StaffContext!.OrganizationId,
                cancellationToken);
            return Results.Ok(ToPreference(branch));
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewUpdateStatus);

        organizations.MapPut("branches/{branchId:guid}/updates/preferences", async (
            Guid branchId,
            UpdateOrganizationAdminUpdatePreferenceRequest request,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageBranchSettings, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            if (request.MaintenanceWindowStart == request.MaintenanceWindowEnd)
                return Results.BadRequest(new { Error = "Maintenance window start and end must be different." });
            var branch = await dbContext.Branches.SingleAsync(
                candidate => candidate.BranchId == branchId && candidate.OrganizationId == request.OrganizationId,
                cancellationToken);
            branch.OrganizationAdminMaintenanceWindowStart = request.MaintenanceWindowStart;
            branch.OrganizationAdminMaintenanceWindowEnd = request.MaintenanceWindowEnd;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToPreference(branch));
        });

        organizations.MapGet("branches/{branchId:guid}/updates/rollouts", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewUpdateStatus, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId,
                    authorization.StaffContext.StaffUserId, AuditActionNames.ViewUpdateRollout, "UpdateRollout",
                    null, AuditOutcome.Denied, new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await updateService.ListRolloutStatusesAsync(
                authorization.StaffContext!.OrganizationId, branchId, cancellationToken);
            if (!result.Succeeded) return ToUpdateHttpResult(result);
            await WriteAuditAsync(auditRecordWriter, authorization.StaffContext.OrganizationId, branchId,
                authorization.StaffContext.StaffUserId, AuditActionNames.ViewUpdateRollout, "UpdateRollout",
                null, AuditOutcome.Succeeded, new { Count = result.Response!.Count }, cancellationToken);
            return Results.Ok(result.Response);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewUpdateStatus);

        organizations.MapGet("branches/{branchId:guid}/updates/rollouts/{rolloutId:guid}", async (
            Guid branchId,
            Guid rolloutId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IUpdateService updateService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewUpdateStatus, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var result = await updateService.GetRolloutAsync(
                authorization.StaffContext!.OrganizationId, branchId, rolloutId, cancellationToken);
            if (!result.Succeeded) return ToUpdateHttpResult(result);
            await WriteAuditAsync(auditRecordWriter, authorization.StaffContext.OrganizationId, branchId,
                authorization.StaffContext.StaffUserId, AuditActionNames.ViewUpdateRollout, "UpdateRollout",
                rolloutId.ToString("D"), AuditOutcome.Succeeded, new { result.Response!.State }, cancellationToken);
            return Results.Ok(result.Response);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewUpdateStatus);

        app.MapPost("/api/devices/{deviceId:guid}/updates/check", async (
            Guid deviceId,
            DeviceUpdateCheckRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IUpdateService updateService,
            IOrganizationStatusGuard organizationStatusGuard,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            var secret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, secret))
                return Results.Unauthorized();
            var suspended = await organizationStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspended is not null) return suspended;
            return ToUpdateHttpResult(await updateService.CheckForUpdatesAsync(request, cancellationToken));
        });

        app.MapPost("/api/devices/{deviceId:guid}/updates/status", async (
            Guid deviceId,
            DeviceUpdateStatusReportRequest request,
            HttpContext httpContext,
            IDeviceCredentialValidator credentialValidator,
            IUpdateService updateService,
            IOrganizationStatusGuard organizationStatusGuard,
            CancellationToken cancellationToken) =>
        {
            if (deviceId != request.DeviceId)
                return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
            var secret = httpContext.Request.Headers[DeviceCredentialHeaders.CredentialSecret].SingleOrDefault();
            if (!credentialValidator.ValidateApproved(request.OrganizationId, request.BranchId, deviceId, secret))
                return Results.Unauthorized();
            var suspended = await organizationStatusGuard.RequireActiveAsync(request.OrganizationId, cancellationToken);
            if (suspended is not null) return suspended;
            return ToUpdateHttpResult(await updateService.ReportStatusAsync(request, cancellationToken));
        });

        app.MapHub<DeviceHub>("/hubs/devices");
    }

    private static OrganizationAdminUpdatePreferenceDto ToPreference(BranchEntity branch) => new(
        branch.OrganizationId,
        branch.BranchId,
        branch.OrganizationAdminMaintenanceWindowStart,
        branch.OrganizationAdminMaintenanceWindowEnd,
        branch.PreferredTimeZone);
}
