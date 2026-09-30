using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Sessions;

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

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z");

    [Fact]
    public void ExpiresAt_ActiveSessionWithPaidEnd_CoversThePaidEnd() =>
        Assert.Equal(
            Now.AddMinutes(50),
            GraceLeasePolicy.ExpiresAt(SessionStateNames.Active, Now.AddMinutes(50), Now, graceMinutes: 15));

    [Fact]
    public void ExpiresAt_PaidEndCloserThanGrace_StillEndsAtThePaidEnd() =>
        Assert.Equal(
            Now.AddMinutes(3),
            GraceLeasePolicy.ExpiresAt(SessionStateNames.Active, Now.AddMinutes(3), Now, graceMinutes: 15));

    [Fact]
    public void ExpiresAt_OpenSession_FallsBackToGraceWindow() =>
        Assert.Equal(
            Now.AddMinutes(15),
            GraceLeasePolicy.ExpiresAt(SessionStateNames.Active, endsAtUtc: null, Now, graceMinutes: 15));

    [Fact]
    public void ExpiresAt_PaidEndAlreadyPassed_FallsBackToGraceWindow() =>
        Assert.Equal(
            Now.AddMinutes(15),
            GraceLeasePolicy.ExpiresAt(SessionStateNames.Active, Now.AddMinutes(-1), Now, graceMinutes: 15));

    // На паузе конец сдвигается на время простоя: подписывать его нельзя.
    [Theory]
    [InlineData(SessionStateNames.Paused)]
    [InlineData(SessionStateNames.Ending)]
    public void ExpiresAt_NotActive_FallsBackToGraceWindow(string state) =>
        Assert.Equal(
            Now.AddMinutes(15),
            GraceLeasePolicy.ExpiresAt(state, Now.AddMinutes(50), Now, graceMinutes: 15));
}
