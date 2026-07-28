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
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Operator;
using AFK4.Shared.Contracts.Platform.SupportNotes;
using AFK4.Shared.Contracts.Platform.Organizations;
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

namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    public static async Task<IResult> ExportReportCsvAsync<TReport>(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        StaffAuthorizationService authorizationService,
        IAuditRecordWriter auditRecordWriter,
        IReportService reportService,
        string auditAction,
        string targetId,
        string fileName,
        Func<IReportService, Guid, Guid, ReportSearchQuery, CancellationToken, Task<TReport>> loadReportAsync,
        Func<TReport, string> exportCsv,
        CancellationToken cancellationToken,
        Guid? actorStaffUserId = null,
        long? minAmountMinorUnits = null,
        long? maxAmountMinorUnits = null)
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
                auditAction,
                "Report",
                targetId,
                AuditOutcome.Denied,
                new { Format = "csv", authorization.DenialReason },
                cancellationToken);

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = new ReportSearchQuery(fromUtc, toUtc, limit, actorStaffUserId, minAmountMinorUnits, maxAmountMinorUnits);
        var result = await loadReportAsync(
            reportService,
            authorization.StaffContext!.OrganizationId,
            branchId,
            query,
            cancellationToken);
        var csv = exportCsv(result);

        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext.OrganizationId,
            branchId,
            authorization.StaffContext.StaffUserId,
            auditAction,
            "Report",
            targetId,
            AuditOutcome.Succeeded,
            new
            {
                Format = "csv",
                Count = GetReportRowCount(result),
                fromUtc,
                toUtc,
                limit
            },
            cancellationToken);

        return Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv; charset=utf-8",
            fileDownloadName: fileName);
    }

    public static int GetReportRowCount<TReport>(TReport report)
    {
        return report switch
        {
            ShiftReportResultDto shiftReport => shiftReport.Rows.Count,
            SalesReportResultDto salesReport => salesReport.Rows.Count,
            GameplayTimeReportResultDto gameplayTimeReport => gameplayTimeReport.Rows.Count,
            CashOperationReportResultDto cashOperationReport => cashOperationReport.Rows.Count,
            OperatorActionReportResultDto operatorActionReport => operatorActionReport.Rows.Count,
            _ => 0
        };
    }
}
