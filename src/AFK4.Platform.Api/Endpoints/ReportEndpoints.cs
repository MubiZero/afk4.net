using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/reports/shifts", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
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
                    AuditActionNames.ViewShiftReport,
                    "Report",
                    "shifts",
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new ReportSearchQuery(fromUtc, toUtc, limit);
            var result = await reportService.GetShiftReportAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewShiftReport,
                "Report",
                "shifts",
                AuditOutcome.Succeeded,
                new
                {
                    Count = result.Rows.Count,
                    result.Limit,
                    fromUtc,
                    toUtc
                },
                cancellationToken);

            return Results.Ok(result);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/sales", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
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
                    AuditActionNames.ViewSalesReport,
                    "Report",
                    "sales",
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new ReportSearchQuery(fromUtc, toUtc, limit);
            var result = await reportService.GetSalesReportAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewSalesReport,
                "Report",
                "sales",
                AuditOutcome.Succeeded,
                new
                {
                    Count = result.Rows.Count,
                    result.Limit,
                    fromUtc,
                    toUtc
                },
                cancellationToken);

            return Results.Ok(result);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/gameplay-time", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
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
                    AuditActionNames.ViewGameplayTimeReport,
                    "Report",
                    "gameplay-time",
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new ReportSearchQuery(fromUtc, toUtc, limit);
            var result = await reportService.GetGameplayTimeReportAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewGameplayTimeReport,
                "Report",
                "gameplay-time",
                AuditOutcome.Succeeded,
                new
                {
                    Count = result.Rows.Count,
                    result.Limit,
                    fromUtc,
                    toUtc
                },
                cancellationToken);

            return Results.Ok(result);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/cash-operations", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
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
                    AuditActionNames.ViewCashOperationReport,
                    "Report",
                    "cash-operations",
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new ReportSearchQuery(fromUtc, toUtc, limit);
            var result = await reportService.GetCashOperationReportAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewCashOperationReport,
                "Report",
                "cash-operations",
                AuditOutcome.Succeeded,
                new
                {
                    Count = result.Rows.Count,
                    result.Limit,
                    fromUtc,
                    toUtc
                },
                cancellationToken);

            return Results.Ok(result);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/operator-actions", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            Guid? actorStaffUserId,
            long? minAmountMinorUnits,
            long? maxAmountMinorUnits,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
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
                    AuditActionNames.ViewOperatorActionReport,
                    "Report",
                    "operator-actions",
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new ReportSearchQuery(fromUtc, toUtc, limit, actorStaffUserId, minAmountMinorUnits, maxAmountMinorUnits);
            var result = await reportService.GetOperatorActionReportAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewOperatorActionReport,
                "Report",
                "operator-actions",
                AuditOutcome.Succeeded,
                new
                {
                    Count = result.Rows.Count,
                    result.Limit,
                    fromUtc,
                    toUtc,
                    actorStaffUserId,
                    minAmountMinorUnits,
                    maxAmountMinorUnits
                },
                cancellationToken);

            return Results.Ok(result);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/shifts/export.csv", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return await ExportReportCsvAsync(
                branchId,
                fromUtc,
                toUtc,
                limit,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewShiftReport,
                "shifts",
                "afk4-shifts-report.csv",
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetShiftReportAsync(organizationId, scopedBranchId, query, token),
                ReportCsvExporter.ExportShiftReport,
                cancellationToken);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/sales/export.csv", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return await ExportReportCsvAsync(
                branchId,
                fromUtc,
                toUtc,
                limit,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewSalesReport,
                "sales",
                "afk4-sales-report.csv",
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetSalesReportAsync(organizationId, scopedBranchId, query, token),
                ReportCsvExporter.ExportSalesReport,
                cancellationToken);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/gameplay-time/export.csv", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return await ExportReportCsvAsync(
                branchId,
                fromUtc,
                toUtc,
                limit,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewGameplayTimeReport,
                "gameplay-time",
                "afk4-gameplay-time-report.csv",
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetGameplayTimeReportAsync(organizationId, scopedBranchId, query, token),
                ReportCsvExporter.ExportGameplayTimeReport,
                cancellationToken);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/cash-operations/export.csv", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return await ExportReportCsvAsync(
                branchId,
                fromUtc,
                toUtc,
                limit,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewCashOperationReport,
                "cash-operations",
                "afk4-cash-operations-report.csv",
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetCashOperationReportAsync(organizationId, scopedBranchId, query, token),
                ReportCsvExporter.ExportCashOperationReport,
                cancellationToken);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/operator-actions/export.csv", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            Guid? actorStaffUserId,
            long? minAmountMinorUnits,
            long? maxAmountMinorUnits,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            return await ExportReportCsvAsync(
                branchId,
                fromUtc,
                toUtc,
                limit,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewOperatorActionReport,
                "operator-actions",
                "afk4-operator-actions-report.csv",
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetOperatorActionReportAsync(organizationId, scopedBranchId, query, token),
                ReportCsvExporter.ExportOperatorActionReport,
                cancellationToken,
                actorStaffUserId,
                minAmountMinorUnits,
                maxAmountMinorUnits);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

    }
}
