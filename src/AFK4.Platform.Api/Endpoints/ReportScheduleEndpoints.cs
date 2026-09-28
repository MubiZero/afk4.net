using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reports;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class ReportScheduleEndpoints
{
    public static void MapReportScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("branches/{branchId:guid}/report-schedules", async (
            Guid branchId,
            CreateReportScheduleRequest request,
            StaffAuthorizationService authorizationService,
            EfReportScheduleService reportScheduleService,
            IAuditRecordWriter auditRecordWriter,
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
                    AuditActionNames.CreateReportSchedule,
                    "ReportSchedule",
                    null,
                    AuditOutcome.Denied,
                    new { request.ReportType, request.Frequency, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var validation = ValidateCreateReportScheduleRequest(request);
            if (validation is not null)
            {
                return Results.BadRequest(new { Error = validation });
            }

            // Проверка «уже есть такая» — не защита от гонки (два одновременных запроса пройдут
            // оба; уникального индекса на эту тройку в схеме нет), а защита от обычного случая:
            // человек не помнит, заводил ли он уже эту рассылку, и заводит вторую.
            if (await reportScheduleService.ExistsAsync(
                request.OrganizationId, branchId, request.ReportType, request.Frequency, cancellationToken))
            {
                return Results.Conflict(new
                {
                    Error = "schedule_exists",
                    Message = "This branch already has a schedule for the same report and frequency."
                });
            }

            var dto = await reportScheduleService.CreateAsync(
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                request.ReportType,
                request.Frequency,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateReportSchedule,
                "ReportSchedule",
                dto.ReportScheduleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.ReportType, request.Frequency },
                cancellationToken);

            return Results.Ok(dto);
        });

        app.MapGet("branches/{branchId:guid}/report-schedules", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            EfReportScheduleService reportScheduleService,
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
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var schedules = await reportScheduleService.ListAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            return Results.Ok(schedules);
        })
            .AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReports);

        // Пауза и правка частоты. До этого рассылку можно было только завести или убрать: `IsActive` в
        // ответе был, а переключить его было нечем, и уйти в отпуск значило удалить и завести заново.
        app.MapPatch("branches/{branchId:guid}/report-schedules/{scheduleId:guid}", async (
            Guid branchId,
            Guid scheduleId,
            UpdateReportScheduleRequest request,
            StaffAuthorizationService authorizationService,
            EfReportScheduleService reportScheduleService,
            IAuditRecordWriter auditRecordWriter,
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
                    AuditActionNames.UpdateReportSchedule,
                    "ReportSchedule",
                    scheduleId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Frequency, request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var validation = ValidateUpdateReportScheduleRequest(request);
            if (validation is not null)
            {
                return Results.BadRequest(new { Error = validation });
            }

            var (schedule, error) = await reportScheduleService.UpdateAsync(
                authorization.StaffContext.OrganizationId,
                branchId,
                scheduleId,
                request.Frequency,
                request.IsActive,
                cancellationToken);

            if (error is not null)
            {
                return Results.Conflict(new
                {
                    Error = error,
                    Message = "This branch already has a schedule for the same report and frequency."
                });
            }

            if (schedule is null)
            {
                return Results.NotFound();
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdateReportSchedule,
                "ReportSchedule",
                scheduleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Frequency, request.IsActive },
                cancellationToken);

            return Results.Ok(schedule);
        });

        app.MapDelete("branches/{branchId:guid}/report-schedules/{scheduleId:guid}", async (
            Guid branchId,
            Guid scheduleId,
            StaffAuthorizationService authorizationService,
            EfReportScheduleService reportScheduleService,
            IAuditRecordWriter auditRecordWriter,
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
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var deleted = await reportScheduleService.DeleteAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                scheduleId,
                cancellationToken);

            if (!deleted)
            {
                return Results.NotFound();
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.DeleteReportSchedule,
                "ReportSchedule",
                scheduleId.ToString("D"),
                AuditOutcome.Succeeded,
                new { scheduleId },
                cancellationToken);

            return Results.Ok(new { message = "Report schedule deleted." });
        });

    }
}
