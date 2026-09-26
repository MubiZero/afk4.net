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

    // Подменили накопитель или унесли монитор — агент шлёт сразу, не дожидаясь суточного отчёта.
    [Fact]
    public void TheFingerprint_SeesASwappedDriveAndAMissingMonitor()
    {
        var snapshot = new HardwareSnapshotDto("CPU", 12, 16, [new HardwareGpuDto("GPU", 8)], "Board", [new HardwareDiskDto("C:", 500)], "Windows 11", "BIOS 1",
            PhysicalDisks: [new HardwarePhysicalDiskDto("Samsung SSD 980 PRO 1TB", 1000, "NVMe")],
            Monitors: [new HardwareMonitorDto("S24R35x", "SAM", "H4ZN500123"), new HardwareMonitorDto("DELL P2419H", "DEL", "CFV9N93")]);

        Assert.NotEqual(HardwareSchedule.Fingerprint(snapshot),
            HardwareSchedule.Fingerprint(snapshot with { PhysicalDisks = [new HardwarePhysicalDiskDto("Kingston SA400S37240G", 240, "SATA")] }));
        Assert.NotEqual(HardwareSchedule.Fingerprint(snapshot),
            HardwareSchedule.Fingerprint(snapshot with { Monitors = [new HardwareMonitorDto("S24R35x", "SAM", "H4ZN500123")] }));
        Assert.NotEqual(HardwareSchedule.Fingerprint(snapshot),
            HardwareSchedule.Fingerprint(snapshot with { Monitors = [new HardwareMonitorDto("S24R35x", "SAM", "OTHER"), new HardwareMonitorDto("DELL P2419H", "DEL", "CFV9N93")] }));
        // Не прочитали мониторы — это не «мониторов нет».
        Assert.NotEqual(HardwareSchedule.Fingerprint(snapshot with { Monitors = null }), HardwareSchedule.Fingerprint(snapshot with { Monitors = [] }));
        // Порядок, в котором Windows перечислила мониторы после перезагрузки, — не изменение.
        Assert.Equal(HardwareSchedule.Fingerprint(snapshot),
            HardwareSchedule.Fingerprint(snapshot with { Monitors = [new HardwareMonitorDto("DELL P2419H", "DEL", "CFV9N93"), new HardwareMonitorDto("S24R35x", "SAM", "H4ZN500123")] }));
    }
}

/// <summary>Настоящая Windows: реестр отвечает процессором, памятью и диском C:, диски — своим описанием.</summary>
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
        // Накопители и мониторы прочитались: список может быть пустым (виртуальная машина CI), но не «неизвестно».
        Assert.NotNull(snapshot.PhysicalDisks);
        Assert.NotNull(snapshot.Monitors);
    }
}
