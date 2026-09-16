using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Sessions;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Арифметика паузы. Её читают чекаут, живая сумма на карте и автозащита по долгу — одна ошибка
/// здесь становится ошибкой в деньгах сразу в трёх местах.
/// </summary>
public sealed class SessionPauseTests
{
    private static readonly DateTimeOffset Noon = DateTimeOffset.Parse("2026-09-16T12:00:00Z");

    [Fact]
    public void BillableElapsed_WithoutPauses_IsTheWholeSpan()
    {
        var session = new SessionEntity();

        Assert.Equal(TimeSpan.FromHours(2), SessionPause.BillableElapsed(session, Noon, Noon.AddHours(2)));
    }

    // Ради этого всё и делается: за время, что игрок не играл, он не платит.
    [Fact]
    public void BillableElapsed_SubtractsAClosedPause()
    {
        var session = new SessionEntity { TotalPausedSeconds = 900 };

        Assert.Equal(TimeSpan.FromMinutes(105), SessionPause.BillableElapsed(session, Noon, Noon.AddHours(2)));
    }

    // Пока пауза не снята, счётчик стоит: на карте сумма не растёт, и чекаут посреди паузы
    // посчитает столько же.
    [Fact]
    public void BillableElapsed_FreezesWhileThePauseIsStillOpen()
    {
        var session = new SessionEntity { PausedAtUtc = Noon.AddMinutes(30) };

        var atPause = SessionPause.BillableElapsed(session, Noon, Noon.AddMinutes(30));
        var muchLater = SessionPause.BillableElapsed(session, Noon, Noon.AddHours(3));

        Assert.Equal(TimeSpan.FromMinutes(30), atPause);
        Assert.Equal(atPause, muchLater);
    }

    [Fact]
    public void BillableElapsed_AddsClosedAndOpenPausesTogether()
    {
        var session = new SessionEntity { TotalPausedSeconds = 600, PausedAtUtc = Noon.AddMinutes(50) };

        Assert.Equal(TimeSpan.FromMinutes(40), SessionPause.BillableElapsed(session, Noon, Noon.AddHours(1)));
    }

    // Часы на сервере могут пойти назад: отрицательная пауза стала бы подарком за чей-то счёт.
    [Fact]
    public void PausedTotal_NeverCountsTimeBackwards()
    {
        var session = new SessionEntity { PausedAtUtc = Noon.AddMinutes(10) };

        Assert.Equal(TimeSpan.Zero, SessionPause.PausedTotal(session, Noon));
    }

    [Fact]
    public void BillableElapsed_IsNeverNegative()
    {
        var session = new SessionEntity { TotalPausedSeconds = 7200 };

        Assert.Equal(TimeSpan.Zero, SessionPause.BillableElapsed(session, Noon, Noon.AddMinutes(30)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void ResolveMaxPause_FallsBackToThePlatformDefault(int? configuredMinutes)
    {
        Assert.Equal(SessionPause.DefaultMaxPause, SessionPause.ResolveMaxPause(configuredMinutes));
    }

    [Fact]
    public void ResolveMaxPause_HonoursTheBranchAllowance()
    {
        Assert.Equal(TimeSpan.FromMinutes(45), SessionPause.ResolveMaxPause(45));
    }

    [Fact]
    public void IsPauseExpired_OnlyOnceTheAllowanceIsSpent()
    {
        var session = new SessionEntity { PausedAtUtc = Noon };
        var allowance = TimeSpan.FromMinutes(20);

        Assert.False(SessionPause.IsPauseExpired(session, Noon.AddMinutes(19), allowance));
        Assert.True(SessionPause.IsPauseExpired(session, Noon.AddMinutes(20), allowance));
    }

    [Fact]
    public void IsPauseExpired_ASessionThatIsNotPausedNeverExpires()
    {
        Assert.False(SessionPause.IsPauseExpired(new SessionEntity(), Noon.AddDays(1), TimeSpan.FromMinutes(20)));
    }
}
