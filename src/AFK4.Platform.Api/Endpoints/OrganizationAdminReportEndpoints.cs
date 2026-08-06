using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class OrganizationAdminReportEndpoints
{
    public static void MapOrganizationAdminReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("branches/{branchId:guid}/reports/workspace/summary", async (
            Guid branchId, DateOnly fromDate, DateOnly toDate,
            StaffAuthorizationService authorizationService, IAuditRecordWriter auditRecordWriter,
            IOrganizationAdminReportService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(branchId, "summary", fromDate, toDate, authorizationService, auditRecordWriter,
                (organizationId, token) => service.GetSummaryAsync(organizationId, branchId, fromDate, toDate, token), cancellationToken))
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/workspace/shifts-cash", async (
            Guid branchId, DateOnly fromDate, DateOnly toDate,
            StaffAuthorizationService authorizationService, IAuditRecordWriter auditRecordWriter,
            IOrganizationAdminReportService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(branchId, "shifts-cash", fromDate, toDate, authorizationService, auditRecordWriter,
                (organizationId, token) => service.GetShiftCashAsync(organizationId, branchId, fromDate, toDate, token), cancellationToken));
        // Не помечено AllowPlatformSupportAccess: маршрут содержит "/shifts" и ловится
        // денежным guard-тестом как совпадение с фрагментом запрета для смен/кассы.

        app.MapGet("branches/{branchId:guid}/reports/workspace/revenue", async (
            Guid branchId, DateOnly fromDate, DateOnly toDate,
            StaffAuthorizationService authorizationService, IAuditRecordWriter auditRecordWriter,
            IOrganizationAdminReportService service, CancellationToken cancellationToken) =>
            await ExecuteAsync(branchId, "revenue", fromDate, toDate, authorizationService, auditRecordWriter,
                (organizationId, token) => service.GetRevenueAsync(organizationId, branchId, fromDate, toDate, token), cancellationToken))
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        app.MapGet("branches/{branchId:guid}/reports/workspace/shifts-cash/export.csv", async (
            Guid branchId, DateOnly fromDate, DateOnly toDate,
            StaffAuthorizationService authorizationService, IAuditRecordWriter auditRecordWriter,
            IOrganizationAdminReportService service, CancellationToken cancellationToken) =>
            await ExecuteCsvAsync(branchId, "shifts-cash-csv", fromDate, toDate, authorizationService, auditRecordWriter,
                async (organizationId, token) => OrganizationAdminReportCsvExporter.Export(await service.GetShiftCashAsync(organizationId, branchId, fromDate, toDate, token)), cancellationToken));
        // Не помечено AllowPlatformSupportAccess: маршрут содержит "/shifts" и ловится
        // денежным guard-тестом как совпадение с фрагментом запрета для смен/кассы.

        app.MapGet("branches/{branchId:guid}/reports/workspace/revenue/export.csv", async (
            Guid branchId, DateOnly fromDate, DateOnly toDate,
            StaffAuthorizationService authorizationService, IAuditRecordWriter auditRecordWriter,
            IOrganizationAdminReportService service, CancellationToken cancellationToken) =>
            await ExecuteCsvAsync(branchId, "revenue-csv", fromDate, toDate, authorizationService, auditRecordWriter,
                async (organizationId, token) => OrganizationAdminReportCsvExporter.Export(await service.GetRevenueAsync(organizationId, branchId, fromDate, toDate, token)), cancellationToken))
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);
    }

    private static async Task<IResult> ExecuteAsync<T>(
        Guid branchId, string target, DateOnly fromDate, DateOnly toDate,
        StaffAuthorizationService authorizationService, IAuditRecordWriter auditRecordWriter,
        Func<Guid, CancellationToken, Task<T>> load, CancellationToken cancellationToken)
    {
        var authorization = await authorizationService.RequireBranchPermissionAsync(branchId, OrganizationPermissionNames.ViewReports, cancellationToken);
        if (!authorization.IsAuthenticated) return Results.Unauthorized();
        if (!authorization.IsAllowed)
        {
            await WriteAuditAsync(auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId,
                authorization.StaffContext.StaffUserId, AuditActionNames.ViewOrganizationAdminReports,
                "Report", target, AuditOutcome.Denied,
                new { authorization.DenialReason, fromDate, toDate }, cancellationToken);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        try
        {
            var result = await load(authorization.StaffContext!.OrganizationId, cancellationToken);
            await WriteAuditAsync(auditRecordWriter, authorization.StaffContext.OrganizationId, branchId,
                authorization.StaffContext.StaffUserId, AuditActionNames.ViewOrganizationAdminReports,
                "Report", target, AuditOutcome.Succeeded, new { fromDate, toDate }, cancellationToken);
            return Results.Ok(result);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return Results.BadRequest(new { code = "invalid_report_range", detail = exception.Message });
        }
    }

    private static async Task<IResult> ExecuteCsvAsync(
        Guid branchId, string target, DateOnly fromDate, DateOnly toDate,
        StaffAuthorizationService authorizationService, IAuditRecordWriter auditRecordWriter,
        Func<Guid, CancellationToken, Task<string>> load, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(branchId, target, fromDate, toDate, authorizationService, auditRecordWriter, load, cancellationToken);
        return result is Microsoft.AspNetCore.Http.HttpResults.Ok<string> ok
            ? Results.Text(ok.Value, "text/csv; charset=utf-8")
            : result;
    }
}
