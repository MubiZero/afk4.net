using AFK4.Player.Shell.Input;

namespace AFK4.Player.Shell.Tests.Input;

/// <summary>Простой и активность по времени последнего ввода: «подошли» — сразу, «ушли» — через минуту тишины.</summary>
public sealed class InputActivityTrackerTests
{
    private static InputActivityTracker Tracker() => new(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));

    [Fact]
    public void TheFirstLook_IsAStartingPoint_NotAnEvent()
    {
        Assert.Equal(InputSignal.None, Tracker().Observe(lastInputTick: 1_000, nowTick: 5_000));
    }

    [Fact]
    public void NewInput_IsActivity()
    {
        var tracker = Tracker();
        tracker.Observe(1_000, 5_000);

        Assert.Equal(InputSignal.Activity, tracker.Observe(6_000, 6_100));
    }

    [Fact]
    public void AStreamOfMouseMoves_IsReportedAtMostOnceASecond()
    {
        var tracker = Tracker();
        tracker.Observe(1_000, 1_000);

        Assert.Equal(InputSignal.Activity, tracker.Observe(2_000, 2_000));
        Assert.Equal(InputSignal.None, tracker.Observe(2_250, 2_250));
        Assert.Equal(InputSignal.None, tracker.Observe(2_500, 2_500));
        Assert.Equal(InputSignal.Activity, tracker.Observe(3_100, 3_100));
    }

    [Fact]
    public void AMinuteOfSilence_IsIdle_Once()
    {
        var tracker = Tracker();
        tracker.Observe(1_000, 1_000);

        Assert.Equal(InputSignal.None, tracker.Observe(1_000, 60_000));
        Assert.Equal(InputSignal.Idle, tracker.Observe(1_000, 61_000));
        Assert.Equal(InputSignal.None, tracker.Observe(1_000, 90_000));
    }

    [Fact]
    public void AfterIdle_NewInputArmsItAgain()
    {
        var tracker = Tracker();
        tracker.Observe(1_000, 1_000);
        tracker.Observe(1_000, 61_000);

        Assert.Equal(InputSignal.Activity, tracker.Observe(70_000, 70_000));
        Assert.Equal(InputSignal.Idle, tracker.Observe(70_000, 130_000));
    }

    [Fact]
    public void ThePcThatWasAlreadyIdleAtStart_DoesNotReportIdleAgain()
    {
        var tracker = Tracker();

        tracker.Observe(1_000, 100_000);

        Assert.Equal(InputSignal.None, tracker.Observe(1_000, 101_000));
    }

    [Fact]
    public void TheTickCounterWrappingAfter49Days_IsNotAnHourOfSilence()
    {
        var tracker = Tracker();
        tracker.Observe(uint.MaxValue - 500, uint.MaxValue - 400);

        // Счётчик перешёл через ноль: с ввода прошло 1,5 секунды, а не 49 дней.
        Assert.Equal(InputSignal.None, tracker.Observe(uint.MaxValue - 500, 1_000));
    }
}
