using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class StaffOnboardingEndpoints
{
    public static void MapStaffOnboardingEndpoints(
        this WebApplication app,
        IEndpointRouteBuilder organizations)
    {
        app.MapPost("/api/auth/staff/forgot-password", async (
            StaffForgotPasswordRequest request,
            IStaffPasswordResetService passwordResetService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserNameOrEmail))
            {
                return Results.BadRequest(new { error = "UserNameOrEmail is required." });
            }

            // Anti-enumeration: always report acceptance regardless of whether the account exists.
            await passwordResetService.RequestResetAsync(request.UserNameOrEmail, cancellationToken);
            return Results.Ok(new { message = "If the account exists, a reset email has been sent." });
        }).RequireRateLimiting("staff-reset");

        app.MapPost("/api/auth/staff/reset-password", async (
            StaffResetPasswordRequest request,
            IStaffPasswordResetService passwordResetService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserNameOrEmail))
            {
                return Results.BadRequest(new { error = "UserNameOrEmail is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return Results.BadRequest(new { error = "Code is required." });
            }

            var passwordValidation = ValidateStaffPin(request.NewPassword);
            if (passwordValidation is not null)
            {
                return Results.BadRequest(new { error = passwordValidation });
            }

            var result = await passwordResetService.ResetAsync(
                request.UserNameOrEmail, request.Code, request.NewPassword, cancellationToken);
            return result.Status switch
            {
                ResetPasswordByEmailStatus.Success => Results.Ok(new { message = "Password updated." }),
                ResetPasswordByEmailStatus.InvalidCode => Results.Json(
                    new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
                    statusCode: StatusCodes.Status400BadRequest),
                ResetPasswordByEmailStatus.Expired => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                ResetPasswordByEmailStatus.NoActiveCode => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                ResetPasswordByEmailStatus.TooManyAttempts => Results.Json(
                    new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("staff-reset");

        organizations.MapPost("branches/{branchId:guid}/staff/invites", async (
            Guid branchId,
            CreateStaffInviteRequest request,
            StaffAuthorizationService authorizationService,
            IStaffInviteService staffInviteService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                OrganizationPermissionNames.ManageBranchStaff,
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
                    AuditActionNames.CreateStaffInvite,
                    "StaffInvite",
                    null,
                    AuditOutcome.Denied,
                    new { request.UserName, request.RoleNames, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var validation = ValidateCreateStaffInviteRequest(request);
            if (validation is not null)
            {
                // Номер правит сам человек за мастером установки, поэтому эта причина едет кодом;
                // остальные отказы валидации означают ошибку клиента, а не ввода.
                var validationCode = PhoneNumberNormalizer.Normalize(request.PhoneNumber) is null
                    ? StaffInviteErrorCodeNames.InvalidPhone
                    : null;
                return Results.BadRequest(new { Error = validation, Code = validationCode });
            }

            var result = await staffInviteService.CreateInviteAsync(
                request.OrganizationId,
                branchId,
                request.UserName,
                request.DisplayName,
                request.PhoneNumber,
                request.Email,
                request.RoleNames,
                cancellationToken);

            if (!result.Succeeded)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    request.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.CreateStaffInvite,
                    "StaffInvite",
                    null,
                    AuditOutcome.Denied,
                    new { request.UserName, Error = result.Error },
                    cancellationToken);

                if (result.PlanLimit is not null)
                {
                    return Results.Conflict(new { Error = result.Error, result.PlanLimit.Code, PlanLimit = result.PlanLimit });
                }

                return Results.BadRequest(new { Error = result.Error, Code = result.ErrorCode });
            }

            await WriteAuditAsync(
                auditRecordWriter,
                request.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.CreateStaffInvite,
                "StaffInvite",
                result.StaffInviteId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.UserName, request.RoleNames },
                cancellationToken);

            return Results.Ok(new StaffInviteDto(result.StaffInviteId, result.Code, result.ExpiresAtUtc));
        })
            // Мастер установки заводит сотрудников на месте: проверка версии панели к нему не применяется.
            .AllowNonOrganizationAdminClients();

        // Кого добавили, но кто ещё не входил. Без этого списка выданный код нельзя было ни
        // увидеть, ни отозвать — только добавить человека заново и надеяться на лучшее.
        organizations.MapGet("branches/{branchId:guid}/staff/invites", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IStaffInviteService staffInviteService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageBranchStaff, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, authorization.StaffContext!.OrganizationId, branchId, authorization.StaffContext.StaffUserId,
                    AuditActionNames.ViewStaffInvites, "StaffInvite", null, AuditOutcome.Denied,
                    new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(await staffInviteService.ListPendingAsync(
                authorization.StaffContext!.OrganizationId, branchId, cancellationToken));
        });

        organizations.MapDelete("branches/{branchId:guid}/staff/invites/{staffInviteId:guid}", async (
            Guid branchId,
            Guid staffInviteId,
            StaffAuthorizationService authorizationService,
            IStaffInviteService staffInviteService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ManageBranchStaff, cancellationToken);
            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var staff = authorization.StaffContext!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                    AuditActionNames.RevokeStaffInvite, "StaffInvite", staffInviteId.ToString("D"), AuditOutcome.Denied,
                    new { authorization.DenialReason }, cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!await staffInviteService.RevokeAsync(staff.OrganizationId, branchId, staffInviteId, cancellationToken))
            {
                return Results.NotFound();
            }

            await WriteAuditAsync(
                auditRecordWriter, staff.OrganizationId, branchId, staff.StaffUserId,
                AuditActionNames.RevokeStaffInvite, "StaffInvite", staffInviteId.ToString("D"), AuditOutcome.Succeeded,
                new { }, cancellationToken);
            return Results.NoContent();
        });

        // Сверка кода первого входа до ПИНа. Отказы — те же, что у приёма.
        app.MapPost(StaffAuthRoutes.CheckInvite, async (
            CheckStaffInviteRequest request,
            IStaffInviteService staffInviteService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var result = await staffInviteService.CheckInviteAsync(request.PhoneNumber, request.Code, cancellationToken);
            if (result.Succeeded)
            {
                return Results.NoContent();
            }

            await AuditRefusalAsync(auditRecordWriter, result, cancellationToken);
            return InviteRefusal(result);
        }).RequireRateLimiting("staff-reset");

        // Приём приглашения. Отвечает теми же словами, что сброс пароля по телефону: человек по
        // ту сторону тот же самый, и два разных языка отказов он читал бы как два разных сбоя.
        app.MapPost(StaffAuthRoutes.AcceptInvite, async (
            AcceptStaffInviteRequest request,
            IStaffInviteService staffInviteService,
            IStaffTokenService tokenService,
            IAuditRecordWriter auditRecordWriter,
            PlatformDbContext db,
            CancellationToken cancellationToken) =>
        {
            var passwordValidation = ValidateStaffPin(request.Password);
            if (passwordValidation is not null)
            {
                return Results.BadRequest(new { error = passwordValidation });
            }

            var result = await staffInviteService.AcceptInviteAsync(
                request.PhoneNumber, request.Code, request.Password, cancellationToken);

            if (!result.Succeeded)
            {
                await AuditRefusalAsync(auditRecordWriter, result, cancellationToken);
                return InviteRefusal(result);
            }

            await WriteAuditAsync(
                auditRecordWriter, result.OrganizationId, result.BranchId, result.StaffUserId,
                AuditActionNames.AcceptStaffInvite, "StaffInvite", result.StaffInviteId?.ToString("D"), AuditOutcome.Succeeded,
                new { result.UserName }, cancellationToken);
            var staffUser = await db.StaffUsers.SingleAsync(user => user.StaffUserId == result.StaffUserId, cancellationToken);
            var signIn = await tokenService.IssueAsync(staffUser, cancellationToken);
            return Results.Ok(new AcceptStaffInviteResponse(result.OrganizationId, result.UserName, signIn));
        }).RequireRateLimiting("staff-reset");

        // Неверный код первого входа — след в журнале клуба: три попытки на подбор шести цифр
        // руководитель должен видеть, а не узнать по «код больше не действует» у сотрудника.
        static Task AuditRefusalAsync(IAuditRecordWriter auditRecordWriter, StaffInviteAcceptResult result, CancellationToken cancellationToken) =>
            result.OrganizationId == Guid.Empty
                ? Task.CompletedTask
                : auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                    OrganizationId: result.OrganizationId,
                    BranchId: result.BranchId,
                    ActorStaffUserId: null,
                    Action: AuditActionNames.AcceptStaffInvite,
                    TargetType: "StaffInvite",
                    TargetId: result.StaffInviteId?.ToString("D"),
                    Outcome: AuditOutcome.Denied,
                    SourceApp: "PlatformApi",
                    DetailsJson: JsonSerializer.Serialize(new { Status = result.Status.ToString(), result.RemainingAttempts })),
                    cancellationToken);

        static IResult InviteRefusal(StaffInviteAcceptResult result)
        {
            if (result.PlanLimit is not null)
            {
                return Results.Conflict(new { Error = result.Error, result.PlanLimit.Code, PlanLimit = result.PlanLimit });
            }

            return result.Status switch
            {
                StaffInviteAcceptStatus.InvalidCode => Results.Json(
                    new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
                    statusCode: StatusCodes.Status400BadRequest),
                StaffInviteAcceptStatus.Expired => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                StaffInviteAcceptStatus.NoActiveInvite => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                StaffInviteAcceptStatus.TooManyAttempts => Results.Json(
                    new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        }

    }
}
