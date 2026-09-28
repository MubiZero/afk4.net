using System.Text;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    /// <summary>
    /// Отчёт филиала в JSON: право на отчёты в филиале, отказ и просмотр — в журнал. Пять отчётов
    /// делали это пятью одинаковыми копиями; выгрузка в CSV уже жила так — через соседний помощник.
    /// </summary>
    public static async Task<IResult> GetReportAsync<TReport>(
        Guid branchId,
        StaffAuthorizationService authorizationService,
        IAuditRecordWriter auditRecordWriter,
        IReportService reportService,
        string auditAction,
        string targetId,
        ReportSearchQuery query,
        Func<IReportService, Guid, Guid, ReportSearchQuery, CancellationToken, Task<TReport>> loadReportAsync,
        Func<TReport, object> succeededDetails,
        CancellationToken cancellationToken)
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
                new { authorization.DenialReason },
                cancellationToken);

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = await loadReportAsync(
            reportService,
            authorization.StaffContext!.OrganizationId,
            branchId,
            query,
            cancellationToken);

        await WriteAuditAsync(
            auditRecordWriter,
            authorization.StaffContext.OrganizationId,
            branchId,
            authorization.StaffContext.StaffUserId,
            auditAction,
            "Report",
            targetId,
            AuditOutcome.Succeeded,
            succeededDetails(result),
            cancellationToken);

        return Results.Ok(result);
    }

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
