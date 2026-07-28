using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class StaffMoneyCapPolicyTests
{
    // --- D4/D5 role defaults ---

    [Fact]
    public void Default_ShiftSupervisor_DailyCapped_PerTxnUnlimited()
    {
        var cap = StaffMoneyCapPolicy.DefaultForRole(OrganizationRoleNames.ShiftSupervisor);
        Assert.Null(cap.PerTransactionMinorUnits);
        Assert.Equal(20000, cap.DailyMinorUnits);
    }

    [Fact]
    public void Default_CashierOperator_FullyZeroCapped()
    {
        var cap = StaffMoneyCapPolicy.DefaultForRole(OrganizationRoleNames.Operator);
        Assert.Equal(0, cap.PerTransactionMinorUnits);
        Assert.Equal(0, cap.DailyMinorUnits);
    }

    [Theory]
    [InlineData(OrganizationRoleNames.OrganizationOwner)]
    [InlineData(OrganizationRoleNames.BranchManager)]
    [InlineData("some_unknown_role")]
    public void Default_UncappedRoles_AreUnlimited(string roleName)
    {
        var cap = StaffMoneyCapPolicy.DefaultForRole(roleName);
        Assert.Null(cap.PerTransactionMinorUnits);
        Assert.Null(cap.DailyMinorUnits);
    }

    // --- Combine across an actor's roles: most permissive wins (null = unlimited) ---

    [Fact]
    public void Combine_SingleRole_ReturnsThatCap()
    {
        var combined = StaffMoneyCapPolicy.Combine([new RoleMoneyCap(null, 20000)]);
        Assert.Null(combined.PerTransactionMinorUnits);
        Assert.Equal(20000, combined.DailyMinorUnits);
    }

    [Fact]
    public void Combine_UnlimitedRole_OverridesCappedRole()
    {
        // Supervisor (daily 20000) + Owner (unlimited) ⇒ unlimited.
        var combined = StaffMoneyCapPolicy.Combine([new RoleMoneyCap(null, 20000), RoleMoneyCap.Unlimited]);
        Assert.Null(combined.PerTransactionMinorUnits);
        Assert.Null(combined.DailyMinorUnits);
    }

    [Fact]
    public void Combine_TwoFiniteCaps_TakesTheHigher()
    {
        var combined = StaffMoneyCapPolicy.Combine([new RoleMoneyCap(100, 500), new RoleMoneyCap(200, 300)]);
        Assert.Equal(200, combined.PerTransactionMinorUnits);
        Assert.Equal(500, combined.DailyMinorUnits);
    }

    [Fact]
    public void Combine_NullPerTxnBeatsFinite_ButDailyTakesMax()
    {
        // Cashier (0,0) + Supervisor (null, 20000): per-txn null wins; daily max(0, 20000).
        var combined = StaffMoneyCapPolicy.Combine([new RoleMoneyCap(0, 0), new RoleMoneyCap(null, 20000)]);
        Assert.Null(combined.PerTransactionMinorUnits);
        Assert.Equal(20000, combined.DailyMinorUnits);
    }

    [Fact]
    public void Combine_Empty_IsUnlimited()
    {
        var combined = StaffMoneyCapPolicy.Combine([]);
        Assert.Null(combined.PerTransactionMinorUnits);
        Assert.Null(combined.DailyMinorUnits);
    }
}
