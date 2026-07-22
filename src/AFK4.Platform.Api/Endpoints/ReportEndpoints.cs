using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Dashboard;
using AFK4.Platform.Api.Diagnostics;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Idempotency;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Security;
using AFK4.Platform.Api.Tenancy;
using AFK4.Platform.Api.Updates;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Diagnostics;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Tenants;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Updates;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/branches/{branchId:guid}/reports/shifts", async (
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
                StaffPermissionNames.ViewReports,
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/sales", async (
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
                StaffPermissionNames.ViewReports,
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/gameplay-time", async (
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
                StaffPermissionNames.ViewReports,
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/cash-operations", async (
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
                StaffPermissionNames.ViewReports,
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/operator-actions", async (
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
                StaffPermissionNames.ViewReports,
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
        });

        // Anti-fraud §5.6: on-demand owner daily summary (the report-endpoint fallback to the notification
        // digest). Defaults to the most recently ended UTC day when no date is given.
        app.MapGet("/api/branches/{branchId:guid}/reports/owner-daily-summary", async (
            Guid branchId,
            DateOnly? date,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IReportService reportService,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.ViewReports,
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
                    AuditActionNames.ViewOwnerDailySummaryReport,
                    "Report",
                    "owner-daily-summary",
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var summaryDate = date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1);
            var result = await reportService.GetOwnerDailySummaryAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                summaryDate,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewOwnerDailySummaryReport,
                "Report",
                "owner-daily-summary",
                AuditOutcome.Succeeded,
                new
                {
                    Date = summaryDate.ToString("yyyy-MM-dd"),
                    ActorCount = result.Rows.Count
                },
                cancellationToken);

            return Results.Ok(result);
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/shifts/export.csv", async (
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/sales/export.csv", async (
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/gameplay-time/export.csv", async (
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/cash-operations/export.csv", async (
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
        });

        app.MapGet("/api/branches/{branchId:guid}/reports/operator-actions/export.csv", async (
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
        });

    }
}
