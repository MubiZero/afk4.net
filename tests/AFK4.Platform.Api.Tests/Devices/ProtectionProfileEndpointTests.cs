using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Профиль защиты ПК филиала (спека оболочки, §6.3): Панель правит, агент читает по ключу
/// устройства, версия едет в сердцебиении.
/// </summary>
public sealed class ProtectionProfileEndpointTests
{
    private static string Route =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/settings/protection";

    private static UpdateBranchProtectionProfileRequest Request(
        int expectedVersion = 0,
        IReadOnlyList<string>? hiddenDrives = null,
        IReadOnlyList<string>? urls = null,
        IReadOnlyList<BlockedWindowRuleDto>? windows = null) =>
        new(
            TestIds.OrganizationId,
            expectedVersion,
            BlockRemovableStorage: true,
            BlockBrowserDownloads: true,
            BlockBrowserIncognito: false,
            DisableRunDialog: true,
            hiddenDrives ?? ["d"],
            urls ?? ["example.com"],
            windows ?? [new BlockedWindowRuleDto("Командная строка", null)]);

    [Fact]
    public async Task ABranchThatNeverSetItUp_HasAnEmptyProfileAtVersionZero()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var profile = await client.GetFromJsonAsync<BranchProtectionProfileDto>(Route);

        Assert.Equal(0, profile!.Profile.Version);
        Assert.False(profile.Profile.BlockRemovableStorage);
        Assert.Empty(profile.Profile.UrlBlocklist);
        Assert.Null(profile.UpdatedAtUtc);
    }

    /// <summary>Повторы и регистр не должны давать новую версию и лишний круг перечитывания на всех ПК.</summary>
    [Fact]
    public async Task Saving_BumpsTheVersion_AndTidiesTheLists()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var response = await client.PutAsJsonAsync(Route, Request(
            hiddenDrives: ["e", "D", "d"],
            urls: [" example.com ", "EXAMPLE.com", "casino.tj"],
            windows: [new BlockedWindowRuleDto("  cmd ", " "), new BlockedWindowRuleDto(null, "ConsoleWindowClass")]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<BranchProtectionProfileDto>())!.Profile;
        Assert.Equal(1, saved.Version);
        Assert.Equal(["D", "E"], saved.HiddenDrives);
        Assert.Equal(["example.com", "casino.tj"], saved.UrlBlocklist);
        Assert.Equal([new BlockedWindowRuleDto("cmd", null), new BlockedWindowRuleDto(null, "ConsoleWindowClass")], saved.BlockedWindows);
        Assert.Equal(1, (await client.GetFromJsonAsync<BranchProtectionProfileDto>(Route))!.Profile.Version);
    }

    /// <summary>Двое открыли профиль, первый сохранил — второй не затирает его правку молча.</summary>
    [Fact]
    public async Task SavingOverSomeoneElsesChange_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await client.PutAsJsonAsync(Route, Request(expectedVersion: 0));

        var stale = await client.PutAsJsonAsync(Route, Request(expectedVersion: 0));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains(ProtectionProfileErrorCodeNames.VersionConflict, await stale.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Route, Request(expectedVersion: 1))).StatusCode);
    }

    [Fact]
    public async Task Saving_LeavesATraceInTheAuditLog()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        await client.PutAsJsonAsync(Route, Request());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.AuditRecords.AnyAsync(record =>
            record.Action == AuditActionNames.UpdateProtectionProfile && record.Outcome == AuditOutcome.Succeeded));
    }

    /// <summary>Техник чинит ПК, но правила клуба задают владелец и управляющий.</summary>
    [Fact]
    public async Task ATechnician_CannotChangeTheClubsRules()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(Route, Request())).StatusCode);
    }

    public static TheoryData<UpdateBranchProtectionProfileRequest> Invalid => new()
    {
        Request(hiddenDrives: ["DE"]),
        Request(hiddenDrives: ["1"]),
        Request(urls: ["   "]),
        Request(windows: [new BlockedWindowRuleDto(" ", null)]),
        Request(urls: Enumerable.Range(0, 201).Select(index => $"site{index}.tj").ToList()),
        Request(expectedVersion: -1)
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public async Task ANonsenseProfile_IsRejected(UpdateBranchProtectionProfileRequest request)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Route, request)).StatusCode);
    }

    /// <summary>Агент узнаёт о новом профиле из сердцебиения и забирает его своим ключом.</summary>
    [Fact]
    public async Task TheAgent_SeesTheVersionInTheHeartbeat_AndReadsTheProfileWithItsKey()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        Assert.Equal(0, (await fixture.HeartbeatAsync()).PolicyProfileVersion);

        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync(Route, Request())).StatusCode);
        Assert.Equal(1, (await fixture.HeartbeatAsync()).PolicyProfileVersion);

        var profile = await ReadAsAgentAsync(fixture, fixture.Device.CredentialSecret);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var body = (await profile.Content.ReadFromJsonAsync<ProtectionProfileDto>())!;
        Assert.Equal(1, body.Version);
        Assert.True(body.BlockRemovableStorage);
        Assert.Equal(["D"], body.HiddenDrives);
    }

    [Fact]
    public async Task TheProfile_IsNotGivenWithoutTheDeviceKey()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await ReadAsAgentAsync(fixture, "not-the-key")).StatusCode);
    }

    /// <summary>Отчёт агента доезжает до карточки ПК вместе с текущей версией филиала — видно, отстал ли ПК.</summary>
    [Fact]
    public async Task TheAgentsReport_ShowsUpOnThePcCard_NextToTheBranchVersion()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        await fixture.Client.PutAsJsonAsync(Route, Request());
        await fixture.Client.PutAsJsonAsync(Route, Request(expectedVersion: 1));

        var reported = await ReportAsAgentAsync(fixture, fixture.Device.CredentialSecret, version: 1);
        Assert.Equal(HttpStatusCode.NoContent, reported.StatusCode);

        var card = await fixture.Client.GetFromJsonAsync<DeviceDetailDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{fixture.Device.DeviceId:D}");
        Assert.Equal(1, card!.ProtectionReport!.Version);
        Assert.Equal(2, card.BranchProtectionVersion);
        Assert.Equal(ProtectionItemStatusNames.ExplorerOnly,
            card.ProtectionReport.Items.Single(item => item.Item == ProtectionItemNames.HiddenDrives).Status);
    }

    [Fact]
    public async Task AReport_WithoutTheDeviceKey_IsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await ReportAsAgentAsync(fixture, "not-the-key", version: 1)).StatusCode);
    }

    private static Task<HttpResponseMessage> ReportAsAgentAsync(DevicePlayerFixture fixture, string credentialSecret, int version)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, DeviceProtectionRoutes.Report(fixture.Device.DeviceId))
        {
            Content = JsonContent.Create(new DeviceProtectionReportRequest(
                fixture.Device.OrganizationId, fixture.Device.BranchId, fixture.Device.DeviceId, version, fixture.Clock.GetUtcNow(),
                [
                    new ProtectionItemReportDto(ProtectionItemNames.RemovableStorage, ProtectionItemStatusNames.Applied, null),
                    new ProtectionItemReportDto(ProtectionItemNames.HiddenDrives, ProtectionItemStatusNames.ExplorerOnly, null)
                ]))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        return fixture.Client.SendAsync(message);
    }

    private static Task<HttpResponseMessage> ReadAsAgentAsync(DevicePlayerFixture fixture, string credentialSecret)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Get,
            DeviceProtectionRoutes.Profile(fixture.Device.DeviceId, fixture.Device.OrganizationId, fixture.Device.BranchId));
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        return fixture.Client.SendAsync(message);
    }
}
