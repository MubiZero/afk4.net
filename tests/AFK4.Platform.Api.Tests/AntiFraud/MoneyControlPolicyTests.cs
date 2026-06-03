using AFK4.Platform.Api.AntiFraud;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class MoneyControlPolicyTests
{
    [Fact]
    public void ApprovalThreshold_BranchOverride_WinsOverGlobal() =>
        Assert.Equal(
            8000,
            MoneyControlPolicy.ResolveApprovalThreshold(branchOverrideMinorUnits: 8000, globalDefaultMinorUnits: 5000));

    [Fact]
    public void ApprovalThreshold_NoOverride_UsesGlobalDefault() =>
        Assert.Equal(
            5000,
            MoneyControlPolicy.ResolveApprovalThreshold(branchOverrideMinorUnits: null, globalDefaultMinorUnits: 5000));

    [Fact]
    public void ApprovalThreshold_ZeroOverride_Honored() =>
        // D3 fork: a brand-new untrusted shop may set 0 so every refund needs approval.
        Assert.Equal(
            0,
            MoneyControlPolicy.ResolveApprovalThreshold(branchOverrideMinorUnits: 0, globalDefaultMinorUnits: 5000));

    [Fact]
    public void ApprovalThreshold_NegativeOverride_ClampsToZero() =>
        Assert.Equal(
            0,
            MoneyControlPolicy.ResolveApprovalThreshold(branchOverrideMinorUnits: -100, globalDefaultMinorUnits: 5000));

    [Fact]
    public void CompThreshold_NoOverride_ReusesApprovalThreshold() =>
        // D7: comp threshold reuses the high-risk approval threshold by default.
        Assert.Equal(
            5000,
            MoneyControlPolicy.ResolveCompThreshold(branchCompOverrideMinorUnits: null, effectiveApprovalThresholdMinorUnits: 5000));

    [Fact]
    public void CompThreshold_Override_WinsOverApprovalThreshold() =>
        Assert.Equal(
            3000,
            MoneyControlPolicy.ResolveCompThreshold(branchCompOverrideMinorUnits: 3000, effectiveApprovalThresholdMinorUnits: 5000));

    [Fact]
    public void DiscrepancyTolerance_BranchOverride_WinsOverGlobal() =>
        Assert.Equal(
            500,
            MoneyControlPolicy.ResolveDiscrepancyTolerance(branchOverrideMinorUnits: 500, globalDefaultMinorUnits: 2000));

    [Fact]
    public void DiscrepancyTolerance_NoOverride_UsesGlobalDefault() =>
        Assert.Equal(
            2000,
            MoneyControlPolicy.ResolveDiscrepancyTolerance(branchOverrideMinorUnits: null, globalDefaultMinorUnits: 2000));

    [Fact]
    public void Defaults_MatchSpec() // D3 = 5000, D6 = 2000
    {
        Assert.Equal(5000, MoneyControlPolicy.DefaultApprovalThresholdMinorUnits);
        Assert.Equal(2000, MoneyControlPolicy.DefaultDiscrepancyToleranceMinorUnits);
    }
}
