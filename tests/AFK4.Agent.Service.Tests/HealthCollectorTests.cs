using System.Runtime.Versioning;
using AFK4.Agent.Service.Hardware;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Здоровье ПК на настоящей Windows (CI): сборщик не падает там, где датчиков нет, и отдаёт то, что Windows
/// знает всегда, — нагрузку со второго снимка, память и место на системном томе.
/// </summary>
public sealed class HealthCollectorTests
{
    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void OnWindows_TheAlwaysAvailableReadingsArrive()
    {
        var collector = new WindowsHealthCollector(NullLogger<WindowsHealthCollector>.Instance);

        var first = collector.Collect()!;
        Thread.Sleep(200);
        var second = collector.Collect()!;

        Assert.Null(first.CpuLoadPercent);
        Assert.InRange(second.CpuLoadPercent!.Value, 0, 100);
        Assert.InRange(second.MemoryUsedPercent!.Value, 1, 100);
        Assert.Contains(second.Volumes, volume => volume.IsSystem && volume.SizeGb > 0);
    }
}
