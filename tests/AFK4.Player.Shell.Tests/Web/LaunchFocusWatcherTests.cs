using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>
/// Игрок нажал «Играть»: окно игры должно выйти вперёд, а оболочка — отступить. Раньше окно
/// оставалось под оболочкой, и игрок нажимал и не видел ничего.
/// </summary>
public sealed class LaunchFocusWatcherTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-30T10:00:00Z");

    private static IntPtr H(int value) => new(value);

    [Fact]
    public void ANewWindow_AfterTheLaunch_ComesToTheFront()
    {
        var watcher = new LaunchFocusWatcher(TimeSpan.FromSeconds(45));
        watcher.Begin(Now, alreadyOpen: [H(1)]);

        Assert.Null(watcher.Observe(Now.AddSeconds(1), [H(1)]));
        Assert.Equal(H(2), watcher.Observe(Now.AddSeconds(3), [H(1), H(2)]));
    }

    [Fact]
    public void TheSameWindow_IsBroughtForwardOnlyOnce()
    {
        var watcher = new LaunchFocusWatcher(TimeSpan.FromSeconds(45));
        watcher.Begin(Now, []);

        Assert.Equal(H(2), watcher.Observe(Now.AddSeconds(3), [H(2)]));
        // Игрок переключился сам — не тащим окно обратно силой.
        Assert.Null(watcher.Observe(Now.AddSeconds(4), [H(2)]));
    }

    [Fact]
    public void ASecondWindowOfASlowGame_AlsoComesToTheFront()
    {
        // Steam открывается первым, игра — через полминуты.
        var watcher = new LaunchFocusWatcher(TimeSpan.FromSeconds(45));
        watcher.Begin(Now, []);

        Assert.Equal(H(2), watcher.Observe(Now.AddSeconds(3), [H(2)]));
        Assert.Equal(H(3), watcher.Observe(Now.AddSeconds(30), [H(2), H(3)]));
    }

    [Fact]
    public void AfterTheWatchRunsOut_NothingIsPulledToTheFront()
    {
        var watcher = new LaunchFocusWatcher(TimeSpan.FromSeconds(45));
        watcher.Begin(Now, []);

        Assert.Null(watcher.Observe(Now.AddSeconds(46), [H(2)]));
    }

    [Fact]
    public void WithoutALaunch_ItDoesNothing()
    {
        Assert.Null(new LaunchFocusWatcher(TimeSpan.FromSeconds(45)).Observe(Now, [H(2)]));
    }
}
