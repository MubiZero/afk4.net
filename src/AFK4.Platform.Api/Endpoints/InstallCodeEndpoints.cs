using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Install;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Тихая установка по коду (план P5f-2). Код выдаёт в Панели тот, кто вправе ставить ПК, — владелец
/// или техник; ПК предъявляет его сам, без входа сотрудника. В журнал код не пишется никогда:
/// только его идентификатор.
/// </summary>
internal static class InstallCodeEndpoints
{
    private const string TargetType = "InstallCode";

    public static void MapInstallCodeEndpoints(this WebApplication app, IEndpointRouteBuilder organizations)
    {
        organizations.MapGet("branches/{branchId:guid}/install-codes", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IInstallCodeService installCodes,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.InstallDevice, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await installCodes.ListActiveAsync(
                authorization.StaffContext!.OrganizationId, branchId, cancellationToken));
        });

        organizations.MapPost("branches/{branchId:guid}/install-codes", async (
            Guid branchId,
            CreateInstallCodeRequest request,
            StaffAuthorizationService authorizationService,
            IInstallCodeService installCodes,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.InstallDevice, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreateInstallCode, TargetType, null, AuditOutcome.Denied,
                    new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            var result = await installCodes.CreateAsync(staff.OrganizationId, branchId, staff.StaffUserId, request, cancellationToken);
            if (result.Succeeded)
            {
                await WriteAuditAsync(
                    auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                    AuditActionNames.CreateInstallCode, TargetType, result.Value!.InstallCodeId.ToString("D"), AuditOutcome.Succeeded,
                    new { request.LifetimeHours, request.MaxDevices, result.Value.ExpiresAtUtc }, cancellationToken);
            }

            return ToInstallHttpResult(result);
        });

        organizations.MapDelete("branches/{branchId:guid}/install-codes/{installCodeId:guid}", async (
            Guid branchId,
            Guid installCodeId,
            StaffAuthorizationService authorizationService,
            IInstallCodeService installCodes,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.InstallDevice, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.RevokeInstallCode, TargetType, installCodeId.ToString("D"), AuditOutcome.Denied,
                    new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staff = authorization.StaffContext!;
            if (!await installCodes.RevokeAsync(staff.OrganizationId, branchId, installCodeId, cancellationToken))
            {
                return Results.NotFound();
            }

            await WriteAuditAsync(
                auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                AuditActionNames.RevokeInstallCode, TargetType, installCodeId.ToString("D"), AuditOutcome.Succeeded,
                new { }, cancellationToken);
            return Results.NoContent();
        });

        // Сотрудника здесь нет: ПК ставят скриптом развёртывания, и дверь открывает сам код.
        app.MapPost(InstallRoutes.CodeEnroll, async (
            InstallCodeEnrollRequest request,
            HttpContext httpContext,
            IInstallService installService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var result = await installService.EnrollByCodeAsync(request, cancellationToken);

            // Без организации отказ писать некуда: неизвестный код ни к какому клубу не относится.
            if (result.OrganizationId is { } organizationId && result.BranchId is { } branchId)
            {
                var enrollment = result.Value;
                await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: organizationId,
                    BranchId: branchId,
                    ActorStaffUserId: null,
                    Action: result.Succeeded ? AuditActionNames.InstallEnrollSucceeded : AuditActionNames.InstallEnrollRejected,
                    TargetType: "Device",
                    TargetId: enrollment?.Response.DeviceId.ToString("D"),
                    Outcome: result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                    SourceApp: "SetupWizard",
                    DetailsJson: JsonSerializer.Serialize(new
                    {
                        Via = "install_code",
                        IssuedByStaffUserId = result.StaffUserId,
                        enrollment?.InstallCodeId,
                        enrollment?.NewDevice,
                        enrollment?.Response.EnrollmentState,
                        enrollment?.Response.AssignedSeatName,
                        request.SeatName,
                        request.MachineName,
                        result.Error,
                        SourceIp = GetSourceIp(httpContext)
                    })),
                    cancellationToken);
            }

            return result.Succeeded ? Results.Ok(result.Value!.Response) : ToInstallHttpResult(result);
        }).RequireRateLimiting("install-code");
    }
}
