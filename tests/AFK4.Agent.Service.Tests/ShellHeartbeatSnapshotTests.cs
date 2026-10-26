using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Утро после отключения света: ПК загрузился, интернета ещё нет. Экран должен знать, что это
/// «PC-012 · Основной зал» этого клуба, — раньше он узнавал это только из первого сердцебиения
/// (увидено на приёмке в VM 26.10.2026).
/// </summary>
public sealed class ShellHeartbeatSnapshotTests
{
    [Fact]
    public void ThePlaceAndTheClub_SurviveARestartWithoutNetwork_TheSeatingCodeDoesNot()
    {
        using var directory = new TemporaryDirectory();
        var beforeOutage = new ShellHeartbeatSnapshot(directory.Path);
        beforeOutage.Record("418207", DateTimeOffset.UtcNow.AddMinutes(5), new ShellBrandingDto("Клуб «Орион»", null, "#C8FF00"), 15);
        beforeOutage.RecordPlace(new DeviceSeatDto("PC-012", "Основной зал"), null, ["games", "bar"]);

        var afterReboot = new ShellHeartbeatSnapshot(directory.Path);

        Assert.Equal(new DeviceSeatDto("PC-012", "Основной зал"), afterReboot.Seat);
        Assert.Equal("Клуб «Орион»", afterReboot.Branding?.ClubName);
        Assert.Equal(["games", "bar"], afterReboot.Features);
        // Код посадки и интервал — только от живого сервера: устаревший код звал бы гостя в никуда.
        Assert.Null(afterReboot.SeatingCode);
        Assert.Null(afterReboot.IntervalSeconds);
    }

    [Fact]
    public void ABrokenFile_StartsLikeBeforeTheFirstHeartbeat()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(System.IO.Path.Combine(directory.Path, ShellHeartbeatSnapshot.FileName), "{не json");

        var snapshot = new ShellHeartbeatSnapshot(directory.Path);

        Assert.Null(snapshot.Seat);
        Assert.Null(snapshot.Branding);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("afk4-shell-place-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
