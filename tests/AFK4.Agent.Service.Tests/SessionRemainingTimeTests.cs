using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// «Осталось» — до конца сессии: аренда подписана на 15 минут и продлевается, и отсчёт от неё на
/// двухчасовой сессии прыгал между 5 и 15 минутами, а последняя минута не наступала вовсе.
/// </summary>
public sealed class SessionRemainingTimeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-27T10:00:00Z");
    private static readonly Guid SessionId = Guid.NewGuid();

    private static SessionLeaseDto Lease(int minutes) => new(
        SessionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Active", 1,
        Now, Now.AddMinutes(minutes), "ECDSA-P256-SHA256", "sig");

    [Fact]
    public void Online_CountsToTheSessionEnd()
    {
        var remaining = PlayerShellStateBuilder.RemainingSeconds(
            Lease(15), new DeviceLiveSessionDto(SessionId, Now.AddMinutes(-30), Now.AddHours(2)), isOnline: true, Now);

        Assert.Equal(2 * 3600, remaining);
    }

    [Fact]
    public void Offline_TheSessionLivesNoLongerThanItsLease()
    {
        var remaining = PlayerShellStateBuilder.RemainingSeconds(
            Lease(15), new DeviceLiveSessionDto(SessionId, Now.AddMinutes(-30), Now.AddHours(2)), isOnline: false, Now);

        Assert.Equal(15 * 60, remaining);
    }

    [Fact]
    public void AnOpenTab_HasNoCountdownOnline_AndTheLeaseOffline()
    {
        var open = new DeviceLiveSessionDto(SessionId, Now.AddMinutes(-30), EndsAtUtc: null);

        Assert.Null(PlayerShellStateBuilder.RemainingSeconds(Lease(15), open, isOnline: true, Now));
        Assert.Equal(15 * 60, PlayerShellStateBuilder.RemainingSeconds(Lease(15), open, isOnline: false, Now));
    }

    [Fact]
    public void NoLease_NoCountdown()
    {
        Assert.Null(PlayerShellStateBuilder.RemainingSeconds(null, null, isOnline: true, Now));
    }
}
