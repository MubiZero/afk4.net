using AFK4.Agent.Service.Power;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Свет пропал — сессия не пропала (docs/roadmap/power-outage-resume.md): агент замечает, что ПК был обесточен,
/// и не путает это с выключением Windows или перезапуском своей службы.
/// </summary>
public sealed class PowerLossTrackerTests : IDisposable
{
    private static readonly DateTimeOffset Evening = DateTimeOffset.Parse("2026-10-28T15:00:00Z");
    private readonly string directory = Directory.CreateTempSubdirectory("afk4-power-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void TheFirstStart_ReportsNothing() =>
        Assert.Null(new PowerLossTracker(directory, () => 5_000).PowerLostFromUtc);

    [Fact]
    public void RebootedWithoutACleanStop_MeansThePowerWasLost_SinceTheLastAliveMoment()
    {
        new PowerLossTracker(directory, () => 3_600_000).MarkAlive(Evening);

        var afterBoot = new PowerLossTracker(directory, () => 40_000);

        Assert.Equal(Evening, afterBoot.PowerLostFromUtc);
        Assert.Equal(40, afterBoot.UptimeSeconds);
    }

    [Fact]
    public void AWindowsShutdown_IsNotAPowerLoss()
    {
        var running = new PowerLossTracker(directory, () => 3_600_000);
        running.MarkAlive(Evening);
        running.MarkCleanStop(Evening.AddSeconds(5));

        Assert.Null(new PowerLossTracker(directory, () => 40_000).PowerLostFromUtc);
    }

    // Служба перезапустилась (обновление, сбой), а машина работала: аптайм только вырос — света это не касается.
    [Fact]
    public void AServiceRestartWithoutReboot_IsNotAPowerLoss()
    {
        new PowerLossTracker(directory, () => 3_600_000).MarkAlive(Evening);

        Assert.Null(new PowerLossTracker(directory, () => 3_700_000).PowerLostFromUtc);
    }

    // Связи после включения ещё нет, а служба успела перезапуститься: отметка не теряется, пока сервер её не принял.
    [Fact]
    public void TheMarkSurvivesAnAgentRestart_UntilTheServerHasIt()
    {
        new PowerLossTracker(directory, () => 3_600_000).MarkAlive(Evening);
        var afterBoot = new PowerLossTracker(directory, () => 40_000);
        afterBoot.MarkAlive(Evening.AddMinutes(20));

        var restarted = new PowerLossTracker(directory, () => 90_000);
        Assert.Equal(Evening, restarted.PowerLostFromUtc);

        restarted.Reported();
        Assert.Null(restarted.PowerLostFromUtc);
        Assert.Null(new PowerLossTracker(directory, () => 95_000).PowerLostFromUtc);
    }
}
