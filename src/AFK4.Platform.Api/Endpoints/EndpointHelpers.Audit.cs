using System.Text.Json;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static partial class EndpointHelpers
{
    public static bool TryParseMoneyActionType(string? actionType, out MoneyActionType requestedType, out string requiredPermission)
    {
        switch (actionType?.Trim().ToLowerInvariant())
        {
            case MoneyActionTypeNames.Refund:
                requestedType = MoneyActionType.Refund;
                requiredPermission = OrganizationPermissionNames.RefundLedgerEntry;
                return true;
            case MoneyActionTypeNames.ManualCorrection:
                requestedType = MoneyActionType.ManualCorrection;
                requiredPermission = OrganizationPermissionNames.ManualLedgerCorrection;
                return true;
            default:
                requestedType = default;
                requiredPermission = string.Empty;
                return false;
        }
    }

    public static async Task<IReadOnlyCollection<string>> GetActorRoleNamesAsync(
        PlatformDbContext dbContext,
        Guid staffUserId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await dbContext.StaffRoleAssignments
            .AsNoTracking()
            .Where(role => role.StaffUserId == staffUserId && role.OrganizationId == organizationId)
            .Select(role => role.RoleName)
            .Distinct()
            .ToListAsync(cancellationToken);

    // Anti-fraud §5.2 enforcement: the legacy direct ledger endpoints share the same MoneyActionGuard as
    // the /money-actions front door. Returns null when the action may execute immediately (under threshold,
    // under cap) so the caller proceeds with its direct ledger write; otherwise returns the blocking result
    // (409 — must go through the approval front door; 422 — over cap) and writes the denied audit trail.
    public static async Task<IResult?> GuardLegacyMoneyActionAsync(
        PlatformDbContext dbContext,
        IMoneyActionPolicyResolver policyResolver,
        IAuditRecordWriter auditRecordWriter,
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        MoneyActionType requestedType,
        string accountType,
        long signedAmountMinorUnits,
        CancellationToken cancellationToken)
    {
        var roleNames = await GetActorRoleNamesAsync(dbContext, actorStaffUserId, organizationId, cancellationToken);
        var assessment = await policyResolver.AssessAsync(
            organizationId, branchId, actorStaffUserId, roleNames,
            requestedType, accountType, signedAmountMinorUnits, cancellationToken);

        if (assessment.Decision == MoneyActionDecision.ExecuteNow)
        {
            return null;
        }

        var amount = Math.Abs(signedAmountMinorUnits);
        var requiresApproval = assessment.Decision == MoneyActionDecision.RequireApproval;
        var blockedReason = requiresApproval
            ? "Amount exceeds the approval threshold; submit via /money-actions for manager approval."
            : "Amount exceeds the configured per-transaction or daily cap.";

        await WriteAuditAsync(
            auditRecordWriter,
            organizationId,
            branchId,
            actorStaffUserId,
            AuditActionNames.MoneyActionRequested,
            "MoneyAction",
            null,
            AuditOutcome.Denied,
            new { Decision = assessment.Decision.ToString(), Amount = amount, Reason = blockedReason },
            cancellationToken,
            amountMinorUnits: amount);

        return requiresApproval
            ? Results.Conflict(new { Error = blockedReason, RequiresApproval = true })
            : Results.Json(new { Error = blockedReason }, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    public static async Task WriteAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        string action,
        string targetType,
        string? targetId,
        string outcome,
        object details,
        CancellationToken cancellationToken,
        long? amountMinorUnits = null)
    {
        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            organizationId,
            branchId,
            actorStaffUserId,
            action,
            targetType,
            targetId,
            outcome,
            "PlatformApi",
            JsonSerializer.Serialize(details))
        {
            AmountMinorUnits = amountMinorUnits
        },
            cancellationToken);
    }

    public static async Task WritePlatformAuditAsync(
        IAuditRecordWriter auditRecordWriter,
        Guid organizationId,
        Guid? actorPlatformAdminUserId,
        string action,
        string targetType,
        string? targetId,
        string outcome,
        object details,
        CancellationToken cancellationToken)
    {
        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            organizationId,
            null,
            null,
            action,
            targetType,
            targetId,
            outcome,
            "PlatformApi",
            JsonSerializer.Serialize(details))
        {
            ActorPlatformAdminUserId = actorPlatformAdminUserId
        },
            cancellationToken);
    }
}
