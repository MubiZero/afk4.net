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
using AFK4.Shared.Contracts.Platform.Organizations;
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
