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
