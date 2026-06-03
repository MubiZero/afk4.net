using System.Text.Json;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// Implements the §5.2 approval workflow. Gates a submitted action through the
/// <see cref="IMoneyActionPolicyResolver"/>, holds over-threshold actions as <c>pending</c>
/// <see cref="MoneyActionRequestEntity"/> rows, and on approval replays the verbatim payload through
/// the <see cref="IMoneyActionExecutor"/> (the verified ledger path). No self-approval; double-approve
/// is idempotent; pending requests expire after <see cref="PendingTtl"/>.
/// </summary>
public sealed class MoneyActionApprovalService(
    PlatformDbContext dbContext,
    IMoneyActionPolicyResolver policyResolver,
    IMoneyActionExecutor executor,
    TimeProvider timeProvider) : IMoneyActionApprovalService
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromHours(24);

    public async Task<MoneyActionRequestResult> RequestAsync(
        Guid organizationId,
        Guid branchId,
        Guid shiftId,
        Guid actorStaffUserId,
        IReadOnlyCollection<string> actorRoleNames,
        MoneyActionCommand command,
        CancellationToken cancellationToken)
    {
        var assessment = await policyResolver.AssessAsync(
            organizationId, branchId, actorStaffUserId, actorRoleNames,
            command.RequestedActionType, command.AccountType, command.SignedAmountMinorUnits, cancellationToken);

        switch (assessment.Decision)
        {
            case MoneyActionDecision.Reject:
                return MoneyActionRequestResult.Refused(
                    "This action would exceed your money-action cap for this shift. Ask a manager to approve it.");

            case MoneyActionDecision.ExecuteNow:
                var execution = await executor.ExecuteAsync(
                    organizationId, branchId, actorStaffUserId, assessment.EffectiveActionType, command, cancellationToken);
                return execution.Succeeded
                    ? MoneyActionRequestResult.Executed(execution.ResultingLedgerEntryId)
                    : MoneyActionRequestResult.Failed(execution);

            default:
                var now = timeProvider.GetUtcNow();
                var request = new MoneyActionRequestEntity
                {
                    MoneyActionRequestId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    ShiftId = shiftId,
                    ActionType = MoneyActionTypeNames.From(assessment.EffectiveActionType),
                    RequestedByStaffUserId = actorStaffUserId,
                    PayloadJson = JsonSerializer.Serialize(command),
                    AmountMinorUnits = assessment.AmountMinorUnits,
                    CurrencyCode = command.CurrencyCode,
                    Reason = command.Reason,
                    State = MoneyActionRequestStateNames.Pending,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = now + PendingTtl,
                };
                dbContext.MoneyActionRequests.Add(request);
                await dbContext.SaveChangesAsync(cancellationToken);
                return MoneyActionRequestResult.Pending(request.MoneyActionRequestId);
        }
    }

    public async Task<MoneyActionDecisionResult> ApproveAsync(
        Guid organizationId,
        Guid branchId,
        Guid moneyActionRequestId,
        Guid approverStaffUserId,
        string? decisionReason,
        CancellationToken cancellationToken)
    {
        var request = await LoadAsync(organizationId, branchId, moneyActionRequestId, cancellationToken);
        if (request is null)
        {
            return MoneyActionDecisionResult.Missing("Money action request not found.");
        }

        // Double-approve collapses to the original result (§7).
        if (request.State == MoneyActionRequestStateNames.Approved)
        {
            return MoneyActionDecisionResult.Ok(request.ResultingLedgerEntryId);
        }

        if (request.State != MoneyActionRequestStateNames.Pending)
        {
            return MoneyActionDecisionResult.RequestConflict($"Request is already {request.State}.");
        }

        var now = timeProvider.GetUtcNow();
        if (now > request.ExpiresAtUtc)
        {
            request.State = MoneyActionRequestStateNames.Expired;
            request.DecidedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return MoneyActionDecisionResult.RequestConflict("Request has expired; the operator must resubmit.");
        }

        // No self-approval — even with the permission (§7, D2).
        if (approverStaffUserId == request.RequestedByStaffUserId)
        {
            return MoneyActionDecisionResult.Denied("You cannot approve your own money action.");
        }

        var command = JsonSerializer.Deserialize<MoneyActionCommand>(request.PayloadJson)
            ?? throw new InvalidOperationException($"Money action request '{request.MoneyActionRequestId}' has an unreadable payload.");
        var effectiveType = MoneyActionGuard.Classify(command.RequestedActionType, command.AccountType, command.SignedAmountMinorUnits);

        var execution = await executor.ExecuteAsync(
            organizationId, branchId, request.RequestedByStaffUserId, effectiveType, command, cancellationToken);
        if (!execution.Succeeded)
        {
            // Leave the request pending so it can be retried or expire; do not consume it on a transient failure.
            return new MoneyActionDecisionResult(false, null, execution.Error, execution.NotFound, execution.Conflict, false);
        }

        request.State = MoneyActionRequestStateNames.Approved;
        request.ApprovedByStaffUserId = approverStaffUserId;
        request.DecisionReason = decisionReason;
        request.ResultingLedgerEntryId = execution.ResultingLedgerEntryId;
        request.DecidedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return MoneyActionDecisionResult.Ok(execution.ResultingLedgerEntryId);
    }

    public async Task<MoneyActionDecisionResult> RejectAsync(
        Guid organizationId,
        Guid branchId,
        Guid moneyActionRequestId,
        Guid approverStaffUserId,
        string? decisionReason,
        CancellationToken cancellationToken)
    {
        var request = await LoadAsync(organizationId, branchId, moneyActionRequestId, cancellationToken);
        if (request is null)
        {
            return MoneyActionDecisionResult.Missing("Money action request not found.");
        }

        if (request.State != MoneyActionRequestStateNames.Pending)
        {
            return MoneyActionDecisionResult.RequestConflict($"Request is already {request.State}.");
        }

        request.State = MoneyActionRequestStateNames.Rejected;
        request.ApprovedByStaffUserId = approverStaffUserId;
        request.DecisionReason = decisionReason;
        request.DecidedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        return MoneyActionDecisionResult.Ok(null);
    }

    public async Task<IReadOnlyList<MoneyActionRequestEntity>> ListPendingAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken) =>
        await dbContext.MoneyActionRequests
            .AsNoTracking()
            .Where(request =>
                request.OrganizationId == organizationId
                && request.BranchId == branchId
                && request.State == MoneyActionRequestStateNames.Pending)
            .OrderBy(request => request.CreatedAtUtc)
            .ThenBy(request => request.MoneyActionRequestId)
            .ToListAsync(cancellationToken);

    private Task<MoneyActionRequestEntity?> LoadAsync(
        Guid organizationId,
        Guid branchId,
        Guid moneyActionRequestId,
        CancellationToken cancellationToken) =>
        dbContext.MoneyActionRequests.SingleOrDefaultAsync(
            request =>
                request.MoneyActionRequestId == moneyActionRequestId
                && request.OrganizationId == organizationId
                && request.BranchId == branchId,
            cancellationToken);
}
