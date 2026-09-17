using AFK4.Agent.Service;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Аренда приходит с платформы абсолютным временем, а часы игрового ПК врут регулярно. Отстающие
/// часы дарят гостю время, спешащие запирают оплаченную машину.
/// </summary>
public sealed class PlatformSyncedTimeProviderTests
{
    private static readonly DateTimeOffset ServerNow = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public void BeforeTheFirstHeartbeat_TheClockIsTheMachineClock()
    {
        var machineClock = new MutableTimeProvider(ServerNow.AddHours(-3));
        var clock = new PlatformSyncedTimeProvider(machineClock);

        Assert.Equal(TimeSpan.Zero, clock.Offset);
        Assert.Equal(machineClock.GetUtcNow(), clock.GetUtcNow());
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(3)]
    public void AfterAHeartbeat_TheClockFollowsThePlatform(int machineHoursOff)
    {
        var machineClock = new MutableTimeProvider(ServerNow.AddHours(machineHoursOff));
        var clock = new PlatformSyncedTimeProvider(machineClock);

        clock.Synchronize(ServerNow);

        Assert.Equal(ServerNow, clock.GetUtcNow());
        Assert.Equal(TimeSpan.FromHours(-machineHoursOff), clock.Offset);
    }

    // Поправка считается от часов машины, а не от уже поправленных: иначе второе сердцебиение
    // складывало бы её саму с собой и уносило время всё дальше.
    [Fact]
    public void RepeatedHeartbeats_DoNotStackTheCorrection()
    {
        var machineClock = new MutableTimeProvider(ServerNow.AddHours(-3));
        var clock = new PlatformSyncedTimeProvider(machineClock);

        clock.Synchronize(ServerNow);
        machineClock.Advance(TimeSpan.FromMinutes(5));
        clock.Synchronize(ServerNow.AddMinutes(5));

        Assert.Equal(ServerNow.AddMinutes(5), clock.GetUtcNow());
    }

    // Длительности считаются по тем же часам с обоих концов, поэтому поправка в них сокращается.
    [Fact]
    public void ADuration_IsNotDistortedByTheCorrection()
    {
        var machineClock = new MutableTimeProvider(ServerNow.AddHours(-3));
        var clock = new PlatformSyncedTimeProvider(machineClock);
        clock.Synchronize(ServerNow);

        var start = clock.GetUtcNow();
        machineClock.Advance(TimeSpan.FromMinutes(7));

        Assert.Equal(TimeSpan.FromMinutes(7), clock.GetUtcNow() - start);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan by) => current += by;
    }
}
