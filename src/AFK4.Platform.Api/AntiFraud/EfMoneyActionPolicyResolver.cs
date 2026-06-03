using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// EF-backed <see cref="IMoneyActionPolicyResolver"/> (anti-fraud spec §5.1/§5.3). Reads the branch's
/// approval threshold, resolves the actor's effective caps (per-branch override row ?? code default,
/// combined most-permissive across roles), measures the actor's high-risk spend in the open shift,
/// and hands the lot to the pure <see cref="MoneyActionGuard"/>.
/// </summary>
public sealed class EfMoneyActionPolicyResolver(
    PlatformDbContext dbContext,
    IOpenShiftResolver openShiftResolver) : IMoneyActionPolicyResolver
{
    public async Task<MoneyActionAssessment> AssessAsync(
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        IReadOnlyCollection<string> actorRoleNames,
        MoneyActionType requestedActionType,
        string accountType,
        long signedAmountMinorUnits,
        CancellationToken cancellationToken)
    {
        var effectiveType = MoneyActionGuard.Classify(requestedActionType, accountType, signedAmountMinorUnits);
        var amount = Math.Abs(signedAmountMinorUnits);

        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.BranchId == branchId, cancellationToken);

        var threshold = MoneyControlPolicy.ResolveApprovalThreshold(
            branch?.HighRiskApprovalThresholdMinorUnits,
            MoneyControlPolicy.DefaultApprovalThresholdMinorUnits);

        var combinedCap = await ResolveCapsAsync(branchId, actorRoleNames, cancellationToken);

        var spentToday = await GetSpentTodayAsync(organizationId, branchId, actorStaffUserId, cancellationToken);

        var policy = new MoneyActionPolicy(
            ApprovalThresholdMinorUnits: threshold,
            PerTransactionCapMinorUnits: combinedCap.PerTransactionMinorUnits,
            DailyCapMinorUnits: combinedCap.DailyMinorUnits);

        var decision = MoneyActionGuard.Evaluate(amount, spentToday, policy);

        return new MoneyActionAssessment(effectiveType, decision, policy, amount, spentToday);
    }

    private async Task<RoleMoneyCap> ResolveCapsAsync(
        Guid branchId,
        IReadOnlyCollection<string> actorRoleNames,
        CancellationToken cancellationToken)
    {
        var overrides = await dbContext.StaffMoneyCaps
            .AsNoTracking()
            .Where(cap => cap.BranchId == branchId && cap.ActionScope == MoneyActionScopes.HighRisk)
            .ToListAsync(cancellationToken);

        var roleCaps = actorRoleNames.Select(role =>
        {
            var match = overrides.FirstOrDefault(cap => cap.RoleName == role);
            return match is null
                ? StaffMoneyCapPolicy.DefaultForRole(role)
                : new RoleMoneyCap(match.PerTransactionMinorUnits, match.DailyMinorUnits);
        });

        return StaffMoneyCapPolicy.Combine(roleCaps);
    }

    private async Task<long> GetSpentTodayAsync(
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        CancellationToken cancellationToken)
    {
        var openShift = await openShiftResolver.GetOpenShiftIdAsync(organizationId, branchId, cancellationToken);
        if (!openShift.Succeeded || openShift.Response == Guid.Empty)
        {
            return 0;
        }

        var ledgerSpent = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.ShiftId == openShift.Response
                && entry.CreatedByStaffUserId == actorStaffUserId
                && MoneyActionEntryTypes.HighRisk.Contains(entry.EntryType))
            .SumAsync(entry => (long?)Math.Abs(entry.AmountMinorUnits), cancellationToken) ?? 0;

        // §5.4: comps carry no ledger entry, so their assessed value is summed from the
        // session.comp audit feed. Audits hold no ShiftId, so the window is bounded by when
        // the open shift opened — keeping comps in the actor's daily high-risk spend.
        var shiftOpenedAtUtc = await dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.ShiftId == openShift.Response)
            .Select(shift => (DateTimeOffset?)shift.OpenedAtUtc)
            .SingleOrDefaultAsync(cancellationToken);

        if (shiftOpenedAtUtc is null)
        {
            return ledgerSpent;
        }

        var compSpent = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(record =>
                record.OrganizationId == organizationId
                && record.BranchId == branchId
                && record.ActorStaffUserId == actorStaffUserId
                && record.Action == AuditActionNames.SessionComp
                && record.AmountMinorUnits != null
                && record.CreatedAtUtc >= shiftOpenedAtUtc.Value)
            .SumAsync(record => (long?)Math.Abs(record.AmountMinorUnits!.Value), cancellationToken) ?? 0;

        return ledgerSpent + compSpent;
    }
}
