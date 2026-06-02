using AFK4.Platform.Api.Sessions;

namespace AFK4.Platform.Api.Tests;

public sealed class GraceLeasePolicyTests
{
    [Fact]
    public void Resolve_BranchOverride_WinsOverGlobal() =>
        Assert.Equal(30, GraceLeasePolicy.Resolve(branchOverrideMinutes: 30, globalDefaultMinutes: 15));

    [Fact]
    public void Resolve_NoBranchOverride_UsesGlobalDefault() =>
        Assert.Equal(15, GraceLeasePolicy.Resolve(branchOverrideMinutes: null, globalDefaultMinutes: 15));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Resolve_BelowMinimum_ClampsToMinimum(int branchOverride) =>
        Assert.Equal(GraceLeasePolicy.MinGraceMinutes, GraceLeasePolicy.Resolve(branchOverride, globalDefaultMinutes: 15));

    [Fact]
    public void Resolve_AboveMaximum_ClampsToMaximum() =>
        Assert.Equal(GraceLeasePolicy.MaxGraceMinutes, GraceLeasePolicy.Resolve(branchOverrideMinutes: 500, globalDefaultMinutes: 15));

    [Fact]
    public void Resolve_GlobalDefaultAlsoClamped() =>
        Assert.Equal(GraceLeasePolicy.MaxGraceMinutes, GraceLeasePolicy.Resolve(branchOverrideMinutes: null, globalDefaultMinutes: 9999));
}
