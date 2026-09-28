using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Shifts;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class ShiftEndpoints
{
    public static void MapShiftEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("branches/{branchId:guid}/shifts/open", async (
            Guid branchId,
            OpenShiftRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.OpenShift,
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
                    AuditActionNames.OpenShift,
                    "Shift",
                    null,
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await shiftService.OpenShiftAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.OpenShift,
                "Shift",
                result.Response!.ShiftId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.StartingCash },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("branches/{branchId:guid}/shifts/current", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            EfShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ViewShift,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await shiftService.GetCurrentShiftAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                cancellationToken);

            return result.Response is null
                ? Results.NotFound()
                : Results.Ok(result.Response);
        });

        app.MapPost("shifts/{shiftId:guid}/cash-movements", async (
            Guid shiftId,
            RecordCashMovementRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            var shift = await LoadShiftScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                shiftId,
                OrganizationPermissionNames.ManageShiftCash,
                cancellationToken);
            if (shift.Result is not null)
            {
                return shift.Result;
            }

            var authorization = shift.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    shift.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.RecordCashMovement,
                    "Shift",
                    shiftId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await shiftService.RecordCashMovementAsync(
                shiftId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                shift.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.RecordCashMovement,
                "CashMovement",
                result.Response!.CashMovementId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.MovementType, request.Amount },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapGet("branches/{branchId:guid}/shifts/revenue/current", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewReports, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await reportService.GetCurrentShiftRevenueAsync(
                authorization.StaffContext!.OrganizationId, branchId, cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapGet("branches/{branchId:guid}/shifts/revenue", async (
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            StaffAuthorizationService authorizationService,
            IReportService reportService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewReports, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var query = new ReportSearchQuery(fromUtc, toUtc, limit);
            var result = await reportService.GetShiftRevenueAsync(
                authorization.StaffContext!.OrganizationId, branchId, query, cancellationToken);

            return Results.Ok(result);
        });

        app.MapPost("shifts/{shiftId:guid}/close", async (
            Guid shiftId,
            CloseShiftRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            EfShiftService shiftService,
            CancellationToken cancellationToken) =>
        {
            // Грузим по узкому праву: оно есть у всех, у кого есть широкое, плюс у кассира.
            var shift = await LoadShiftScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                shiftId,
                OrganizationPermissionNames.CloseOwnShift,
                cancellationToken);
            if (shift.Result is not null)
            {
                return shift.Result;
            }

            var authorization = shift.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    shift.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CloseShift,
                    "Shift",
                    shiftId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            // Широкое право закрывает любую смену. Без него — только свою: ту, которую сам и
            // открыл. Сверку кассы это не отменяет, и расхождение сверх допуска по-прежнему
            // потребует подписи второго человека (§5.7) — «закрыть поверх недостачи» в одиночку
            // нельзя было и не стало можно.
            var closesAnyShift = await authorizationService.RequireBranchPermissionAsync(
                shift.BranchId,
                OrganizationPermissionNames.CloseShift,
                cancellationToken);

            if (!closesAnyShift.IsAllowed
                && shift.Entity!.OpenedByStaffUserId != authorization.StaffContext.StaffUserId)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext.OrganizationId,
                    shift.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CloseShift,
                    "Shift",
                    shiftId.ToString("D"),
                    AuditOutcome.Denied,
                    new { Reason = "not_own_shift" },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await shiftService.CloseShiftAsync(
                shiftId,
                authorization.StaffContext.StaffUserId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                shift.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CloseShift,
                "Shift",
                shiftId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.CountedCash, result.Response!.Difference },
                cancellationToken);

            // Anti-fraud §5.7: record the manager sign-off as its own audit fact when a discrepancy was cleared.
            if (result.Response.ManagerSignOffStaffUserId is { } signOffStaffUserId)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext.OrganizationId,
                    shift.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.ShiftSignOff,
                    "Shift",
                    shiftId.ToString("D"),
                    AuditOutcome.Succeeded,
                    new { SignOffStaffUserId = signOffStaffUserId, result.Response.Difference, request.SignOffReason },
                    cancellationToken);
            }

            return Results.Ok(result.Response);
        });

    }
}
