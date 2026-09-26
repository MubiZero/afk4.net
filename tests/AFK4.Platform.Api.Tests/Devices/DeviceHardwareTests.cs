using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>Опись железа ПК (P9): что считать изменением и как клуб его принимает.</summary>
public sealed class DeviceHardwareTests
{
    private static HardwareSnapshotDto Snapshot(int memoryGb = 16, string gpu = "NVIDIA GeForce RTX 3060", string os = "Windows 11 Pro 23H2 build 22631") =>
        new("AMD Ryzen 5 5600X 6-Core Processor", 12, memoryGb, [new HardwareGpuDto(gpu, 12)], "ASUSTeK PRIME B550M-A",
            [new HardwareDiskDto("C:", 500), new HardwareDiskDto("D:", 1000)], os, "American Megatrends 2803");

    [Fact]
    public void ANewGpuAndLessMemory_AreChanges_AWindowsUpdateIsNot()
    {
        var changes = DeviceHardware.Diff(Snapshot(), Snapshot(memoryGb: 8, gpu: "NVIDIA GeForce RTX 4060", os: "Windows 11 Pro 24H2 build 26100"));

        Assert.Equal([HardwareComponentNames.Memory, HardwareComponentNames.Gpu], changes.Select(change => change.Component));
        Assert.Equal("16 GB", changes[0].Was);
        Assert.Equal("8 GB", changes[0].Now);
        Assert.Empty(DeviceHardware.Diff(Snapshot(), Snapshot(os: "Windows 11 Pro 24H2 build 26100")));
    }

    [Fact]
    public void DisksAndGpus_AreComparedAsSets_NotByOrder()
    {
        var reordered = Snapshot() with { Disks = [new HardwareDiskDto("D:", 1000), new HardwareDiskDto("C:", 500)] };

        Assert.Empty(DeviceHardware.Diff(Snapshot(), reordered));
    }

    private static readonly HardwarePhysicalDiskDto Nvme = new("Samsung SSD 980 PRO 1TB", 1000, "NVMe");
    private static readonly HardwarePhysicalDiskDto Hdd = new("WDC WD10EZEX-08WN4A0", 1000, "SATA");
    private static readonly HardwareMonitorDto Samsung = new("S24R35x", "SAM", "H4ZN500123");
    private static readonly HardwareMonitorDto Dell = new("DELL P2419H", "DEL", "CFV9N93");

    private static HardwareSnapshotDto WithParts(IReadOnlyList<HardwarePhysicalDiskDto>? disks, IReadOnlyList<HardwareMonitorDto>? monitors) =>
        Snapshot() with { PhysicalDisks = disks, Monitors = monitors };

    [Fact]
    public void ASwappedDrive_IsAChange_ADifferentOrderIsNot()
    {
        var swapped = DeviceHardware.Diff(WithParts([Nvme, Hdd], [Samsung]), WithParts([Nvme, Hdd with { Model = "Kingston SA400S37240G", SizeGb = 240 }], [Samsung]));

        var change = Assert.Single(swapped);
        Assert.Equal(HardwareComponentNames.PhysicalDisk, change.Component);
        Assert.Equal("Samsung SSD 980 PRO 1TB 1000 GB, WDC WD10EZEX-08WN4A0 1000 GB", change.Was);
        Assert.Equal("Kingston SA400S37240G 240 GB, Samsung SSD 980 PRO 1TB 1000 GB", change.Now);
        Assert.Empty(DeviceHardware.Diff(WithParts([Nvme, Hdd], [Samsung, Dell]), WithParts([Hdd, Nvme], [Dell, Samsung])));
    }

    // Монитор унесли — клубу это важно так же, как вынутая видеокарта. Подменили на такой же — видно по серийнику.
    [Fact]
    public void AnUnpluggedOrSwappedMonitor_IsAChange()
    {
        var unplugged = Assert.Single(DeviceHardware.Diff(WithParts([Nvme], [Samsung, Dell]), WithParts([Nvme], [Samsung])));
        Assert.Equal(HardwareComponentNames.Monitor, unplugged.Component);
        Assert.Equal("DELL P2419H (CFV9N93), S24R35x (H4ZN500123)", unplugged.Was);
        Assert.Equal("S24R35x (H4ZN500123)", unplugged.Now);

        var none = Assert.Single(DeviceHardware.Diff(WithParts([Nvme], [Samsung]), WithParts([Nvme], [])));
        Assert.Null(none.Now);

        var swapped = Assert.Single(DeviceHardware.Diff(WithParts([Nvme], [Samsung]), WithParts([Nvme], [Samsung with { Serial = "H4ZN999999" }])));
        Assert.Equal(HardwareComponentNames.Monitor, swapped.Component);
    }

    // Старый агент не знает накопителей и мониторов; не прочиталось — тоже null. Это «неизвестно», а не «всё вынули».
    [Fact]
    public void UnknownDrivesAndMonitors_AreNotChanges_AnEmptyListIs()
    {
        Assert.Empty(DeviceHardware.Diff(Snapshot(), WithParts([Nvme], [Samsung])));
        Assert.Empty(DeviceHardware.Diff(WithParts([Nvme], [Samsung]), WithParts(null, null)));
        Assert.Equal([HardwareComponentNames.PhysicalDisk, HardwareComponentNames.Monitor],
            DeviceHardware.Diff(WithParts([], []), WithParts([Nvme], [Samsung])).Select(change => change.Component));
    }

    // Отметка в списке ПК сравнивает отпечатки; они должны совпадать ровно тогда, когда сверка пуста.
    [Fact]
    public void KnownParts_FillTheGaps_SoTheFingerprintAgreesWithTheDiff()
    {
        var accepted = Snapshot();
        var current = WithParts([Nvme], [Samsung]);

        // Первая опись накопителей и мониторов становится нормой, как когда-то первый снимок.
        var norm = DeviceHardware.WithKnownParts(accepted, current);
        Assert.Equal(current.PhysicalDisks, norm.PhysicalDisks);
        Assert.Equal(current.Monitors, norm.Monitors);
        Assert.Equal(DeviceHardware.Fingerprint(norm), DeviceHardware.Fingerprint(DeviceHardware.WithKnownParts(current, norm)));

        // Потом мониторы не прочитались — отпечаток прежний, отметки нет.
        var unknown = WithParts([Nvme], null);
        Assert.Equal(DeviceHardware.Fingerprint(norm), DeviceHardware.Fingerprint(DeviceHardware.WithKnownParts(unknown, norm)));

        // А унесённый монитор отпечаток меняет.
        Assert.NotEqual(DeviceHardware.Fingerprint(norm), DeviceHardware.Fingerprint(DeviceHardware.WithKnownParts(WithParts([Nvme], []), norm)));

        // Снимок старого агента даёт тот же отпечаток, что и до накопителей с мониторами: обновление
        // сервера не зажигает отметку на ПК, чьи отпечатки уже лежат в базе.
        Assert.Equal("AMD Ryzen 5 5600X 6-Core Processor|16 GB|NVIDIA GeForce RTX 3060 12 GB|ASUSTeK PRIME B550M-A|C: 500 GB, D: 1000 GB",
            DeviceHardware.Fingerprint(accepted));
    }

    // Первый снимок — норма; поменяли видеокарту — это видно в списке и в карточке, пока не примут.
    [Fact]
    public async Task AChangedPc_IsFlagged_UntilSomeoneAcceptsTheNewHardware()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await ReportAsync(fixture, Snapshot())).StatusCode);
        Assert.False(await ChangedInInventoryAsync(fixture));

        await ReportAsync(fixture, Snapshot(gpu: "NVIDIA GeForce RTX 4060"));
        Assert.True(await ChangedInInventoryAsync(fixture));
        var hardware = (await fixture.Client.GetFromJsonAsync<DeviceHardwareDto>(HardwareRoute(fixture)))!;
        var change = Assert.Single(hardware.Changes);
        Assert.Equal(HardwareComponentNames.Gpu, change.Component);

        var accepted = await fixture.Client.PostAsync($"{HardwareRoute(fixture)}/accept", content: null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Empty((await accepted.Content.ReadFromJsonAsync<DeviceHardwareDto>())!.Changes);
        Assert.False(await ChangedInInventoryAsync(fixture));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.AuditRecords.AnyAsync(record => record.Action == AuditActionNames.AcceptDeviceHardware && record.Outcome == AuditOutcome.Succeeded));
    }

    // ПК со снимком старого агента обновился: накопители и мониторы приходят впервые — это не изменение.
    // Унесли монитор после этого — отметка и строка «было → стало»; не прочитались мониторы — отметки нет.
    [Fact]
    public async Task DrivesAndMonitors_ReportedForTheFirstTime_BecomeTheNorm_AndAMissingMonitorIsFlagged()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        await ReportAsync(fixture, Snapshot());
        await ReportAsync(fixture, WithParts([Nvme], [Samsung, Dell]));
        Assert.False(await ChangedInInventoryAsync(fixture));
        Assert.Empty((await fixture.Client.GetFromJsonAsync<DeviceHardwareDto>(HardwareRoute(fixture)))!.Changes);

        await ReportAsync(fixture, WithParts([Nvme], null));
        Assert.False(await ChangedInInventoryAsync(fixture));

        await ReportAsync(fixture, WithParts([Nvme], [Samsung]));
        Assert.True(await ChangedInInventoryAsync(fixture));
        var change = Assert.Single((await fixture.Client.GetFromJsonAsync<DeviceHardwareDto>(HardwareRoute(fixture)))!.Changes);
        Assert.Equal(HardwareComponentNames.Monitor, change.Component);
        Assert.Equal("S24R35x (H4ZN500123)", change.Now);

        var accepted = await fixture.Client.PostAsync($"{HardwareRoute(fixture)}/accept", content: null);
        Assert.Empty((await accepted.Content.ReadFromJsonAsync<DeviceHardwareDto>())!.Changes);
        Assert.False(await ChangedInInventoryAsync(fixture));
    }

    [Fact]
    public async Task AReport_WithoutTheDeviceKey_IsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await ReportAsync(fixture, Snapshot(), "не-тот-ключ")).StatusCode);
    }

    private static string HardwareRoute(DevicePlayerFixture fixture) =>
        $"/api/organizations/{fixture.Device.OrganizationId:D}/devices/{fixture.Device.DeviceId:D}/hardware";

    private static Task<HttpResponseMessage> ReportAsync(DevicePlayerFixture fixture, HardwareSnapshotDto snapshot, string? secret = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, DeviceHardwareRoutes.Report(fixture.Device.DeviceId))
        {
            Content = JsonContent.Create(new DeviceHardwareReportRequest(
                fixture.Device.OrganizationId, fixture.Device.BranchId, fixture.Device.DeviceId, DateTimeOffset.UtcNow, snapshot))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, secret ?? fixture.Device.CredentialSecret);
        return fixture.Client.SendAsync(message);
    }

    private static async Task<bool> ChangedInInventoryAsync(DevicePlayerFixture fixture)
    {
        var inventory = await fixture.Client.GetFromJsonAsync<DeviceInventoryItemDto[]>(
            $"/api/organizations/{fixture.Device.OrganizationId:D}/branches/{fixture.Device.BranchId:D}/devices");
        return inventory!.Single(item => item.DeviceId == fixture.Device.DeviceId).HardwareChanged;
    }
}
