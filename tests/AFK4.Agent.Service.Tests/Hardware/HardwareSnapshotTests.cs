using System.Runtime.Versioning;
using AFK4.Agent.Service.Hardware;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Tests.Hardware;

public sealed class HardwareSnapshotTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T20:00:00Z");

    [Fact]
    public void TheSameHardware_IsSentOnceADay_AChangeAtOnce()
    {
        Assert.True(HardwareSchedule.ShouldReport(null, "a", DateTimeOffset.MinValue, Now));
        Assert.False(HardwareSchedule.ShouldReport("a", "a", Now.AddHours(-23), Now));
        Assert.True(HardwareSchedule.ShouldReport("a", "a", Now.AddHours(-24), Now));
        Assert.True(HardwareSchedule.ShouldReport("a", "b", Now.AddMinutes(-5), Now));
    }

    [Fact]
    public void TheFingerprint_SeesEveryPart()
    {
        var snapshot = new HardwareSnapshotDto("CPU", 12, 16, [new HardwareGpuDto("GPU", 8)], "Board", [new HardwareDiskDto("C:", 500)], "Windows 11", "BIOS 1");

        Assert.NotEqual(HardwareSchedule.Fingerprint(snapshot), HardwareSchedule.Fingerprint(snapshot with { MemoryGb = 8 }));
        Assert.NotEqual(HardwareSchedule.Fingerprint(snapshot), HardwareSchedule.Fingerprint(snapshot with { Gpus = [new HardwareGpuDto("GPU", 12)] }));
    }
}

/// <summary>Настоящая Windows: реестр отвечает процессором, памятью и диском C:.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareSnapshotCollectorTests
{
    [WindowsOnlyFact]
    public void ReadsTheBasics_FromTheRegistry()
    {
        var snapshot = new WindowsHardwareSnapshotCollector().Collect()!;

        Assert.False(string.IsNullOrWhiteSpace(snapshot.Cpu));
        Assert.True(snapshot.CpuThreads > 0);
        Assert.True(snapshot.MemoryGb > 0);
        Assert.Contains(snapshot.Disks, disk => disk.Name.Equals("C:", StringComparison.OrdinalIgnoreCase) && disk.SizeGb > 0);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.Os));
    }
}
