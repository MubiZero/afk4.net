using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Здоровье ПК (docs/operations/pc-health.md): агент раз в пять минут шлёт показатели, сервер помнит худшую
/// проблему — её видят карта мест и карточка ПК.
/// </summary>
public sealed class DeviceHealthTests
{
    private static readonly DeviceHealthDto Healthy = new(
        CpuLoadPercent: 35, MemoryUsedPercent: 60, BoardTemperatureC: 48,
        Gpus: [new GpuHealthDto("NVIDIA GeForce RTX 3060", 71, 45, 90)],
        Disks: [new DiskHealthDto("Samsung SSD 980 1TB", DiskHealthNames.Healthy, 41, 7, 5200, 0)],
        Volumes: [new VolumeSpaceDto("C:", 120, 476, IsSystem: true)]);

    [Theory]
    [InlineData("healthy", null)]
    [InlineData("unhealthy-disk", DeviceHealthIssueNames.DiskFailing)]
    [InlineData("read-errors", DeviceHealthIssueNames.DiskFailing)]
    [InlineData("gpu-95", DeviceHealthIssueNames.GpuOverheating)]
    [InlineData("gpu-88", DeviceHealthIssueNames.GpuHot)]
    [InlineData("worn-ssd", DeviceHealthIssueNames.DiskWorn)]
    [InlineData("full-c", DeviceHealthIssueNames.LowDiskSpace)]
    public void TheWorstIssueIsNamed(string scenario, string? expected)
    {
        var disk = Healthy.Disks[0];
        var health = scenario switch
        {
            "unhealthy-disk" => Healthy with { Disks = [disk with { Health = DiskHealthNames.Unhealthy }] },
            "read-errors" => Healthy with { Disks = [disk with { ReadErrorsUncorrected = 3 }] },
            "gpu-95" => Healthy with { Gpus = [Healthy.Gpus[0] with { TemperatureC = 95 }] },
            "gpu-88" => Healthy with { Gpus = [Healthy.Gpus[0] with { TemperatureC = 88 }] },
            "worn-ssd" => Healthy with { Disks = [disk with { WearPercent = 93 }] },
            "full-c" => Healthy with { Volumes = [new VolumeSpaceDto("C:", 6, 476, IsSystem: true)] },
            _ => Healthy
        };

        Assert.Equal(expected, DeviceHealthRules.WorstIssue(health));
    }

    // Перегрев хуже, чем «мало места», а отказывающий диск — хуже всего: показываем самое важное.
    [Fact]
    public void AFailingDiskOutranksEverythingElse() =>
        Assert.Equal(DeviceHealthIssueNames.DiskFailing, DeviceHealthRules.WorstIssue(Healthy with
        {
            Gpus = [Healthy.Gpus[0] with { TemperatureC = 99 }],
            Disks = [Healthy.Disks[0] with { Health = DiskHealthNames.Unhealthy }],
            Volumes = [new VolumeSpaceDto("C:", 1, 476, IsSystem: true)]
        }));

    [Fact]
    public async Task AReport_ReachesThePcCard_AndTheMap()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var pc = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId, machineName: "PC-07", publicKey: "pc-07");

        await ReportAsync(client, pc, Healthy with { Gpus = [Healthy.Gpus[0] with { TemperatureC = 96 }] });

        var card = await client.GetFromJsonAsync<DeviceHardwareDto>($"/api/organizations/{TestIds.OrganizationId:D}/devices/{pc.DeviceId:D}/hardware");
        Assert.Equal(DeviceHealthIssueNames.GpuOverheating, card!.HealthIssue);
        Assert.Equal(96, card.Health!.Gpus[0].TemperatureC);

        // Остыла — проблема снята.
        await ReportAsync(client, pc, Healthy);
        await using var scope = factory.Services.CreateAsyncScope();
        var device = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Devices.SingleAsync(row => row.DeviceId == pc.DeviceId);
        Assert.Null(device.HealthIssue);
        Assert.Null(device.HealthIssueSinceUtc);
    }

    [Fact]
    public async Task AStrangerCannotReportForAPc()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var pc = await TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId, machineName: "PC-07", publicKey: "pc-07");

        using var message = new HttpRequestMessage(HttpMethod.Post, DeviceHealthRoutes.Report(pc.DeviceId))
        {
            Content = JsonContent.Create(new DeviceHealthReportRequest(TestIds.OrganizationId, TestIds.BranchId, pc.DeviceId, DateTimeOffset.UtcNow, Healthy))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(message)).StatusCode);
    }

    private static async Task ReportAsync(HttpClient client, InstallEnrollResponse pc, DeviceHealthDto health)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, DeviceHealthRoutes.Report(pc.DeviceId))
        {
            Content = JsonContent.Create(new DeviceHealthReportRequest(TestIds.OrganizationId, TestIds.BranchId, pc.DeviceId, DateTimeOffset.UtcNow, health))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, pc.CredentialSecret);
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
