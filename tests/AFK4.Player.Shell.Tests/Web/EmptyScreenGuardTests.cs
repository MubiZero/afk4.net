using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>
/// Экран во время сессии не бывает пустым: оболочка скрыта, окна игры не видно — оболочка
/// возвращается вперёд. Раньше игрок получал тёмный экран, выход из которого знал один Alt+Tab.
/// </summary>
public sealed class EmptyScreenGuardTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z");

    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    [Fact]
    public void NoWindowAnywhere_DuringASession_BringsTheShellBack_AfterTheSettleTime()
    {
        var guard = new EmptyScreenGuard(Settle);

        Assert.False(guard.Observe(sessionRuns: true, shellInFront: false, appWindowVisible: false, Now));
        Assert.False(guard.Observe(true, false, false, Now.AddSeconds(1)));
        Assert.True(guard.Observe(true, false, false, Now.AddSeconds(2)));
    }

    [Fact]
    public void WhenTheFirstAttemptDoesNotTake_ItTriesAgain()
    {
        var guard = new EmptyScreenGuard(Settle);
        guard.Observe(true, false, false, Now);
        Assert.True(guard.Observe(true, false, false, Now.AddSeconds(2)));

        Assert.False(guard.Observe(true, false, false, Now.AddSeconds(3)));
        Assert.True(guard.Observe(true, false, false, Now.AddSeconds(4)));
    }

    [Fact]
    public void AVisibleAppWindow_MeansTheScreenIsNotEmpty()
    {
        var guard = new EmptyScreenGuard(Settle);

        Assert.False(guard.Observe(true, false, appWindowVisible: true, Now));
        Assert.False(guard.Observe(true, false, true, Now.AddSeconds(10)));
    }

    [Fact]
    public void TheShellInFront_IsNotEmpty()
    {
        var guard = new EmptyScreenGuard(Settle);

        Assert.False(guard.Observe(true, shellInFront: true, false, Now));
        Assert.False(guard.Observe(true, true, false, Now.AddSeconds(10)));
    }

    [Fact]
    public void OnALockedPc_ItNeverFires_TheLockScreenIsTheShellsOwnWork()
    {
        var guard = new EmptyScreenGuard(Settle);

        Assert.False(guard.Observe(sessionRuns: false, false, false, Now));
        Assert.False(guard.Observe(false, false, false, Now.AddSeconds(10)));
    }

    [Fact]
    public void AWindowThatAppearsInTime_CancelsTheWait()
    {
        var guard = new EmptyScreenGuard(Settle);
        guard.Observe(true, false, false, Now);
        guard.Observe(true, false, true, Now.AddSeconds(1.5));

        Assert.False(guard.Observe(true, false, false, Now.AddSeconds(2.5)));
    }
}
