using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// A per-transaction + daily money-action cap for one role/scope. A null field means "unlimited"
/// (anti-fraud spec §5.3 — null = unlimited).
/// </summary>
public sealed record RoleMoneyCap(long? PerTransactionMinorUnits, long? DailyMinorUnits)
{
    public static readonly RoleMoneyCap Unlimited = new(null, null);
}

/// <summary>
/// Resolves the effective money-action caps for an actor (anti-fraud spec §5.3, D4/D5). Defaults live
/// in code (the override-in-DB, default-in-code idiom used across branch settings); a per-(branch,
/// role, scope) <c>StaffMoneyCapEntity</c> row, when present, overrides the default for that role.
/// An actor's effective cap combines their roles with **most-permissive-wins**: holding a more
/// privileged role never lowers a limit, and an unlimited role makes the actor unlimited.
/// </summary>
public static class StaffMoneyCapPolicy
{
    /// <summary>D4/D5 defaults. Roles not listed are unlimited (Owner/BranchManager are the approvers).</summary>
    public static RoleMoneyCap DefaultForRole(string roleName) => roleName switch
    {
        StaffRoleNames.ShiftSupervisor => new RoleMoneyCap(PerTransactionMinorUnits: null, DailyMinorUnits: 20000),
        StaffRoleNames.CashierOperator => new RoleMoneyCap(PerTransactionMinorUnits: 0, DailyMinorUnits: 0),
        _ => RoleMoneyCap.Unlimited,
    };

    /// <summary>
    /// Combines the per-role effective caps an actor holds into one cap. For each dimension a null
    /// (unlimited) on any role wins; otherwise the maximum finite value wins. No roles ⇒ unlimited.
    /// </summary>
    public static RoleMoneyCap Combine(IEnumerable<RoleMoneyCap> roleCaps)
    {
        long? perTxn = 0;
        long? daily = 0;
        var any = false;

        foreach (var cap in roleCaps)
        {
            any = true;
            perTxn = MostPermissive(perTxn, cap.PerTransactionMinorUnits);
            daily = MostPermissive(daily, cap.DailyMinorUnits);
        }

        return any ? new RoleMoneyCap(perTxn, daily) : RoleMoneyCap.Unlimited;
    }

    // null (unlimited) wins over any finite value; otherwise the larger finite value wins.
    private static long? MostPermissive(long? accumulated, long? candidate) =>
        accumulated is null || candidate is null ? null : Math.Max(accumulated.Value, candidate.Value);
}
