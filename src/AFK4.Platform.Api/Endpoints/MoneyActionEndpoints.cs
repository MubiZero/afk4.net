using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using static AFK4.Platform.Api.Endpoints.EndpointHelpers;

namespace AFK4.Platform.Api.Endpoints;

internal static class MoneyActionEndpoints
{
    public static void MapMoneyActionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("branches/{branchId:guid}/money-actions", async (
            Guid branchId,
            MoneyActionSubmitRequest request,
            PlatformDbContext dbContext,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IOpenShiftResolver openShiftResolver,
            IMoneyActionApprovalService approvalService,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseMoneyActionType(request.ActionType, out var requestedType, out var requiredPermission))
            {
                return Results.BadRequest(new { Error = "ActionType must be 'refund' or 'manual_correction'." });
            }

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, requiredPermission, cancellationToken);

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
                    AuditActionNames.MoneyActionRequested,
                    "MoneyAction",
                    null,
                    AuditOutcome.Denied,
                    new { request.ActionType, request.SignedAmountMinorUnits, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staffContext = authorization.StaffContext!;
            if (request.OrganizationId != staffContext.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                return Results.BadRequest(new { Error = "IdempotencyKey is required." });
            }

            if (requestedType == MoneyActionType.Refund && request.LedgerEntryId is null)
            {
                return Results.BadRequest(new { Error = "A refund requires the target LedgerEntryId." });
            }

            var openShift = await openShiftResolver.GetOpenShiftIdAsync(
                staffContext.OrganizationId, branchId, cancellationToken);
            if (!openShift.Succeeded || openShift.Response == Guid.Empty)
            {
                return Results.Conflict(new { Error = openShift.Error ?? EfShiftService.OpenShiftRequiredCode });
            }

            var roleNames = await GetActorRoleNamesAsync(
                dbContext, staffContext.StaffUserId, staffContext.OrganizationId, cancellationToken);

            var command = new MoneyActionCommand(
                requestedType,
                request.PlayerAccountId,
                request.LedgerEntryId,
                request.AccountType,
                request.SignedAmountMinorUnits,
                request.CurrencyCode,
                request.QuantitySeconds,
                request.Reason,
                request.IdempotencyKey);

            var result = await approvalService.RequestAsync(
                staffContext.OrganizationId, branchId, openShift.Response, staffContext.StaffUserId,
                roleNames, command, cancellationToken);

            switch (result.Outcome)
            {
                case MoneyActionRequestOutcome.Executed:
                    await WriteAuditAsync(
                        auditRecordWriter,
                        staffContext.OrganizationId,
                        branchId,
                        staffContext.StaffUserId,
                        AuditActionNames.MoneyActionExecuted,
                        "MoneyAction",
                        result.ResultingLedgerEntryId?.ToString("D"),
                        AuditOutcome.Succeeded,
                        new { request.ActionType, request.SignedAmountMinorUnits, request.CurrencyCode },
                        cancellationToken,
                        amountMinorUnits: Math.Abs(request.SignedAmountMinorUnits));
                    return Results.Ok(new MoneyActionSubmitResponse("executed", result.ResultingLedgerEntryId, null));

                case MoneyActionRequestOutcome.PendingApproval:
                    await WriteAuditAsync(
                        auditRecordWriter,
                        staffContext.OrganizationId,
                        branchId,
                        staffContext.StaffUserId,
                        AuditActionNames.MoneyActionRequested,
                        "MoneyAction",
                        result.MoneyActionRequestId?.ToString("D"),
                        AuditOutcome.Succeeded,
                        new { request.ActionType, request.SignedAmountMinorUnits, request.CurrencyCode },
                        cancellationToken,
                        amountMinorUnits: Math.Abs(request.SignedAmountMinorUnits));
                    return Results.Json(
                        new MoneyActionSubmitResponse("pending_approval", null, result.MoneyActionRequestId),
                        statusCode: StatusCodes.Status202Accepted);

                default:
                    if (result.NotFound)
                    {
                        return Results.NotFound(new { result.Error });
                    }

                    return result.Conflict
                        ? Results.Conflict(new { result.Error })
                        : Results.Json(new { result.Error }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        });

        app.MapPost("branches/{branchId:guid}/money-actions/{moneyActionRequestId:guid}/approve", async (
            Guid branchId,
            Guid moneyActionRequestId,
            MoneyActionDecisionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IMoneyActionApprovalService approvalService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ApproveMoneyAction, cancellationToken);

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
                    AuditActionNames.MoneyActionApproved,
                    "MoneyAction",
                    moneyActionRequestId.ToString("D"),
                    AuditOutcome.Denied,
                    new { authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staffContext = authorization.StaffContext!;
            var result = await approvalService.ApproveAsync(
                staffContext.OrganizationId, branchId, moneyActionRequestId,
                staffContext.StaffUserId, request.DecisionReason, cancellationToken);

            if (result.Forbidden)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    staffContext.OrganizationId,
                    branchId,
                    staffContext.StaffUserId,
                    AuditActionNames.MoneyActionApproved,
                    "MoneyAction",
                    moneyActionRequestId.ToString("D"),
                    AuditOutcome.Denied,
                    new { result.Error },
                    cancellationToken);
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (result.NotFound)
            {
                return Results.NotFound(new { result.Error });
            }

            if (result.Conflict)
            {
                return Results.Conflict(new { result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.Json(new { result.Error }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                staffContext.OrganizationId,
                branchId,
                staffContext.StaffUserId,
                AuditActionNames.MoneyActionApproved,
                "MoneyAction",
                moneyActionRequestId.ToString("D"),
                AuditOutcome.Succeeded,
                new { result.ResultingLedgerEntryId },
                cancellationToken);

            return Results.Ok(new MoneyActionSubmitResponse("approved", result.ResultingLedgerEntryId, moneyActionRequestId));
        });

        app.MapPost("branches/{branchId:guid}/money-actions/{moneyActionRequestId:guid}/reject", async (
            Guid branchId,
            Guid moneyActionRequestId,
            MoneyActionDecisionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IMoneyActionApprovalService approvalService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ApproveMoneyAction, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staffContext = authorization.StaffContext!;
            var result = await approvalService.RejectAsync(
                staffContext.OrganizationId, branchId, moneyActionRequestId,
                staffContext.StaffUserId, request.DecisionReason, cancellationToken);

            if (result.NotFound)
            {
                return Results.NotFound(new { result.Error });
            }

            if (result.Conflict)
            {
                return Results.Conflict(new { result.Error });
            }

            if (!result.Succeeded)
            {
                return Results.Json(new { result.Error }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                staffContext.OrganizationId,
                branchId,
                staffContext.StaffUserId,
                AuditActionNames.MoneyActionRejected,
                "MoneyAction",
                moneyActionRequestId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.DecisionReason },
                cancellationToken);

            return Results.Ok(new MoneyActionSubmitResponse("rejected", null, moneyActionRequestId));
        });

        app.MapGet("branches/{branchId:guid}/money-actions", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IMoneyActionApprovalService approvalService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ApproveMoneyAction, cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var staffContext = authorization.StaffContext!;
            var pending = await approvalService.ListPendingAsync(
                staffContext.OrganizationId, branchId, cancellationToken);

            var dtos = pending
                .Select(request => new MoneyActionRequestDto(
                    request.MoneyActionRequestId,
                    request.OrganizationId,
                    request.BranchId,
                    request.ShiftId,
                    request.ActionType,
                    request.RequestedByStaffUserId,
                    request.AmountMinorUnits,
                    request.CurrencyCode,
                    request.Reason,
                    request.State,
                    request.CreatedAtUtc,
                    request.ExpiresAtUtc))
                .ToList();

            return Results.Ok(new MoneyActionRequestListResponse(dtos));
        });

        app.MapPost("players/{playerAccountId:guid}/debts/payments", async (
            Guid playerAccountId,
            PayDebtRequest request,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                OrganizationPermissionNames.PayDebt,
                cancellationToken);
            if (player.Result is not null)
            {
                return player.Result;
            }

            var authorization = player.Authorization!;
            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    player.BranchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.PayDebt,
                    "PlayerAccount",
                    playerAccountId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.Amount, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var inactiveGuard = RejectInactivePlayerMoneyAction(player.Player);
            if (inactiveGuard is not null)
            {
                return inactiveGuard;
            }

            var result = await billingCommandService.PayDebtAsync(
                playerAccountId,
                player.BranchId,
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
                player.BranchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.PayDebt,
                "PlayerAccount",
                playerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.Amount },
                cancellationToken);

            return Results.Ok(result.Response);
        });

    }
}
