using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class DiagnosticsEndpoints
{
    public static void MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/diagnostics", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBranchDiagnosticsService diagnosticsService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewDiagnostics,
                cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewDiagnostics,
                    "Diagnostics",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await diagnosticsService.GetAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            var details = new
            {
                result.DeviceSummary.TotalDevices,
                result.DeviceSummary.StaleDevices,
                result.CommandSummary.PendingCommands,
                result.CommandSummary.FailedCommands,
                result.UpdateSummary.ActiveRollouts,
                result.UpdateSummary.FailedDevices
            };
            await WriteAuditAsync(auditRecordWriter, authorization.StaffContext.OrganizationId,
                branchId, authorization.StaffContext.StaffUserId, AuditActionNames.ViewDiagnostics,
                "Diagnostics", null, AuditOutcome.Succeeded, details, cancellationToken);

            return Results.Ok(result);
        }).RequireOrganizationDomain()
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewDiagnostics);

    }
}
