using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/dashboard/summary", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IOperatorDashboardService dashboardService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewReports,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewDashboardSummary,
                    "Dashboard",
                    "summary",
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new DashboardSummaryQuery(fromUtc, toUtc, limit);
            var result = await dashboardService.GetSummaryAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewDashboardSummary,
                "Dashboard",
                "summary",
                AuditOutcome.Succeeded,
                new
                {
                    FocusQueueCount = result.FocusQueue.Count,
                    RecentPaymentCount = result.RecentPayments.Count,
                    result.AlertPressure.TotalAlerts,
                    fromUtc,
                    toUtc,
                    limit
                },
                cancellationToken);

            return Results.Ok(result);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

    }
}
