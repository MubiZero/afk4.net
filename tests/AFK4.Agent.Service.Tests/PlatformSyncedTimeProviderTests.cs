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

    // Утро после отключения света: батарейка платы села, часы откатились на дату прошивки, роутер
    // ещё не поднялся. Время не опускается ниже запомненного плюс аптайм с перезагрузки.
    [Fact]
    public void AfterPowerLossWithAResetClock_TimeDoesNotFallBelowTheRememberedPlatformTime()
    {
        using var directory = new TemporaryDirectory();
        var beforeOutage = new PlatformSyncedTimeProvider(new MutableTimeProvider(ServerNow), directory.Path, () => 3_600_000);
        beforeOutage.Synchronize(ServerNow);

        var resetMachineClock = new MutableTimeProvider(DateTimeOffset.Parse("2019-01-01T00:00:00Z"));
        var afterReboot = new PlatformSyncedTimeProvider(resetMachineClock, directory.Path, () => 60_000);

        Assert.Equal(ServerNow.AddMinutes(1), afterReboot.GetUtcNow());
    }

    // Служба перезапустилась без перезагрузки: прошло ровно столько, сколько натикал аптайм.
    [Fact]
    public void AServiceRestartWithinTheSameBoot_CountsOnlyTheUptimeSinceTheHeartbeat()
    {
        using var directory = new TemporaryDirectory();
        new PlatformSyncedTimeProvider(new MutableTimeProvider(ServerNow), directory.Path, () => 3_600_000)
            .Synchronize(ServerNow);

        var restarted = new PlatformSyncedTimeProvider(
            new MutableTimeProvider(DateTimeOffset.Parse("2019-01-01T00:00:00Z")), directory.Path, () => 3_720_000);

        Assert.Equal(ServerNow.AddMinutes(2), restarted.GetUtcNow());
    }

    // Граница только снизу: верные часы машины она не трогает, а связь с платформой её отменяет.
    [Fact]
    public void TheFloor_NeitherOverridesACorrectClockNorThePlatform()
    {
        using var directory = new TemporaryDirectory();
        new PlatformSyncedTimeProvider(new MutableTimeProvider(ServerNow), directory.Path, () => 3_600_000)
            .Synchronize(ServerNow);

        var machineClock = new MutableTimeProvider(ServerNow.AddHours(5));
        var clock = new PlatformSyncedTimeProvider(machineClock, directory.Path, () => 60_000);
        Assert.Equal(ServerNow.AddHours(5), clock.GetUtcNow());

        clock.Synchronize(ServerNow.AddMinutes(1));
        Assert.Equal(ServerNow.AddMinutes(1), clock.GetUtcNow());
    }

    // То, ради чего всё: аренда, истёкшая до отключения света, не оживает от откатившихся часов.
    [Fact]
    public void ALeaseThatExpiredBeforeThePowerLoss_DoesNotComeBackWithAResetClock()
    {
        using var directory = new TemporaryDirectory();
        new PlatformSyncedTimeProvider(new MutableTimeProvider(ServerNow), directory.Path, () => 3_600_000)
            .Synchronize(ServerNow);
        new Enforcement.FileSessionLeaseStore(directory.Path, new MutableTimeProvider(ServerNow.AddMinutes(-30)))
            .Save(Lease(expiresAtUtc: ServerNow.AddMinutes(-1)));

        var afterReboot = new PlatformSyncedTimeProvider(
            new MutableTimeProvider(DateTimeOffset.Parse("2019-01-01T00:00:00Z")), directory.Path, () => 60_000);
        var store = new Enforcement.FileSessionLeaseStore(directory.Path, afterReboot);

        Assert.Null(store.Current);
    }

    private static AFK4.Shared.Contracts.Sessions.SessionLeaseDto Lease(DateTimeOffset expiresAtUtc) => new(
        SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
        OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
        SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
        DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
        State: AFK4.Shared.Contracts.Sessions.SessionStateNames.Active,
        Sequence: 1,
        IssuedAtUtc: expiresAtUtc.AddHours(-1),
        ExpiresAtUtc: expiresAtUtc,
        SignatureAlgorithm: "ECDSA-P256-SHA256",
        Signature: "signed-payload");

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("afk4-clock-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan by) => current += by;
    }
}
