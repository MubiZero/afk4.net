using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reports;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/reports/shifts", (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
            GetReportAsync(
                branchId,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewShiftReport,
                "shifts",
                new ReportSearchQuery(fromUtc, toUtc, limit),
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetShiftReportAsync(organizationId, scopedBranchId, query, token),
                result => new { Count = result.Rows.Count, result.Limit, fromUtc, toUtc },
                cancellationToken))
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/sales", (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
            GetReportAsync(
                branchId,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewSalesReport,
                "sales",
                new ReportSearchQuery(fromUtc, toUtc, limit),
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetSalesReportAsync(organizationId, scopedBranchId, query, token),
                result => new { Count = result.Rows.Count, result.Limit, fromUtc, toUtc },
                cancellationToken))
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/gameplay-time", (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
            GetReportAsync(
                branchId,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewGameplayTimeReport,
                "gameplay-time",
                new ReportSearchQuery(fromUtc, toUtc, limit),
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetGameplayTimeReportAsync(organizationId, scopedBranchId, query, token),
                result => new { Count = result.Rows.Count, result.Limit, fromUtc, toUtc },
                cancellationToken))
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/cash-operations", (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            CancellationToken cancellationToken) =>
            GetReportAsync(
                branchId,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewCashOperationReport,
                "cash-operations",
                new ReportSearchQuery(fromUtc, toUtc, limit),
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetCashOperationReportAsync(organizationId, scopedBranchId, query, token),
                result => new { Count = result.Rows.Count, result.Limit, fromUtc, toUtc },
                cancellationToken))
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/operator-actions", (
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
            GetReportAsync(
                branchId,
                authorizationService,
                auditRecordWriter,
                reportService,
                AuditActionNames.ViewOperatorActionReport,
                "operator-actions",
                new ReportSearchQuery(fromUtc, toUtc, limit, actorStaffUserId, minAmountMinorUnits, maxAmountMinorUnits),
                static (service, organizationId, scopedBranchId, query, token) =>
                    service.GetOperatorActionReportAsync(organizationId, scopedBranchId, query, token),
                result => new
                {
                    Count = result.Rows.Count,
                    result.Limit,
                    fromUtc,
                    toUtc,
                    actorStaffUserId,
                    minAmountMinorUnits,
                    maxAmountMinorUnits
                },
                cancellationToken))
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
