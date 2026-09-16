using AFK4.Agent.Service;

namespace AFK4.Agent.Service.Tests;

public sealed class InstalledAppReportScheduleTests
{
    private static readonly DateTimeOffset Noon = DateTimeOffset.Parse("2026-09-16T12:00:00Z");

    [Fact]
    public void IsDue_AtStartup_ReportsImmediately()
    {
        Assert.True(InstalledAppReportSchedule.IsDue(DateTimeOffset.MinValue, Noon, TimeSpan.FromHours(6)));
    }

    [Fact]
    public void IsDue_BeforeTheIntervalElapses_StaysQuiet()
    {
        Assert.False(InstalledAppReportSchedule.IsDue(Noon, Noon.AddHours(5), TimeSpan.FromHours(6)));
    }

    // Ради этого всё и делается: игра, поставленная днём, доезжает до клуба в тот же день, а не
    // после следующей перезагрузки машины.
    [Fact]
    public void IsDue_OnceTheIntervalElapsed_ReportsAgain()
    {
        Assert.True(InstalledAppReportSchedule.IsDue(Noon, Noon.AddHours(6), TimeSpan.FromHours(6)));
    }

    // Реестр игровой машины читается небыстро: настройка «раз в ноль минут» не должна превращать
    // инвентаризацию в бесконечный цикл поверх каждого сердцебиения.
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Interval_NeverGoesBelowAMinute(int configuredMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(1), InstalledAppReportSchedule.Interval(configuredMinutes));
    }

    [Fact]
    public void Interval_DefaultsToTheConfiguredSixHours()
    {
        Assert.Equal(TimeSpan.FromHours(6), InstalledAppReportSchedule.Interval(new AgentOptions().InstalledAppReportIntervalMinutes));
    }
}
