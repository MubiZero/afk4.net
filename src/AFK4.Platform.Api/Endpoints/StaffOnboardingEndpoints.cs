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

            var passwordValidation = ValidateStaffPassword(request.NewPassword);
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
                return Results.BadRequest(new { Error = validation });
            }

            var result = await staffInviteService.CreateInviteAsync(
                request.OrganizationId,
                branchId,
                request.UserName,
                request.DisplayName,
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

                return Results.BadRequest(new { Error = result.Error });
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
        });

        app.MapPost("/api/staff/invites/accept", async (
            AcceptStaffInviteRequest request,
            IStaffInviteService staffInviteService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.BadRequest(new { error = "Token is required." });
            }

            var passwordValidation = ValidateStaffPassword(request.Password);
            if (passwordValidation is not null)
            {
                return Results.BadRequest(new { error = passwordValidation });
            }

            var result = await staffInviteService.AcceptInviteAsync(request.Token, request.Password, cancellationToken);
            return result.Succeeded
                ? Results.Ok(new AcceptStaffInviteResponse(result.OrganizationId, result.UserName))
                : Results.BadRequest(new { error = result.Error });
        });

    }
}
