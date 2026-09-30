using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Sessions;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionLeaseTermTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PaidSession_LeaseRunsToPaidEnd()
    {
        var expires = SessionLeaseTerm.ExpiresAtUtc(Now, Now.AddMinutes(90), SessionStateNames.Active, graceMinutes: 15);

        Assert.Equal(Now.AddMinutes(90), expires);
    }

    [Fact]
    public void VeryLongSession_IsCapped()
    {
        var expires = SessionLeaseTerm.ExpiresAtUtc(Now, Now.AddDays(1), SessionStateNames.Active, graceMinutes: 15);

        Assert.Equal(Now + SessionLeaseTerm.MaxPaidLease, expires);
    }

    [Theory]
    [InlineData(null, SessionStateNames.Active)]
    [InlineData(-5, SessionStateNames.Active)]
    [InlineData(60, SessionStateNames.Paused)]
    public void NoPaidEndAhead_FallsBackToGraceWindow(int? endsInMinutes, string state)
    {
        DateTimeOffset? endsAt = endsInMinutes is { } minutes ? Now.AddMinutes(minutes) : null;

        var expires = SessionLeaseTerm.ExpiresAtUtc(Now, endsAt, state, graceMinutes: 20);

        Assert.Equal(Now.AddMinutes(20), expires);
    }
}
