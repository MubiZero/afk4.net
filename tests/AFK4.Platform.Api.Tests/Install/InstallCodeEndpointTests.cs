using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Install;

/// <summary>Тихая установка по коду: код выдают в Панели, ПК предъявляет его сам.</summary>
public sealed class InstallCodeEndpointTests
{
    private const string PublicKey = "-----BEGIN PUBLIC KEY-----\nsilent-01\n-----END PUBLIC KEY-----";

    [Fact]
    public async Task IssuedCode_IsShownOnce_AndPutsTheMachineOnTheSeatItNames()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var issued = await IssueAsync(client, maxDevices: 30);
        Assert.NotNull(issued.Code);

        var listed = Assert.Single(await ListAsync(client));
        Assert.Equal(issued.InstallCodeId, listed.InstallCodeId);
        Assert.Null(listed.Code);

        using var anonymous = factory.CreateClient();
        var enrolled = await EnrollAsync(anonymous, issued.Code!, seatName: "pc-101");

        Assert.Equal(HttpStatusCode.OK, enrolled.Response.StatusCode);
        Assert.Equal("PC-101", enrolled.Body!.AssignedSeatName);
        Assert.Equal(TestIds.OrganizationId, enrolled.Body.OrganizationId);
        Assert.Equal(TestIds.BranchId, enrolled.Body.BranchId);
        Assert.False(string.IsNullOrWhiteSpace(enrolled.Body.CredentialSecret));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var device = await db.Devices.SingleAsync(row => row.DeviceId == enrolled.Body.DeviceId);
        Assert.Equal(DeviceRoleNames.GamingPc, device.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, device.EnrollmentState);
        Assert.True(await db.DeviceSeatAssignments.AnyAsync(row =>
            row.DeviceId == device.DeviceId && row.SeatId == TestIds.SeatId && row.DetachedAtUtc == null));
        Assert.Equal(1, (await db.InstallCodes.SingleAsync()).UsedDevices);
    }

    // Не названо место — ищется место с именем компьютера: в клубах их обычно так и зовут.
    [Fact]
    public async Task WithoutASeatName_TheMachineNameFindsTheSeat()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client);

        var enrolled = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: null, machineName: "PC-101");

        Assert.Equal("PC-101", enrolled.Body!.AssignedSeatName);
    }

    // Опечатка в имени места не должна оставлять ПК вовсе без регистрации: он встаёт без места,
    // и его привязывают в Панели.
    [Fact]
    public async Task UnknownSeatName_EnrollsTheMachineWithoutASeat()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client);

        var enrolled = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-999");

        Assert.Equal(HttpStatusCode.OK, enrolled.Response.StatusCode);
        Assert.Null(enrolled.Body!.AssignedSeatName);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.DeviceSeatAssignments.AnyAsync(row => row.DeviceId == enrolled.Body.DeviceId));
    }

    [Fact]
    public async Task SeatHeldByAnotherMachine_IsNotTakenAway()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client);
        var first = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");

        var second = await EnrollAsync(
            factory.CreateClient(), issued.Code!, seatName: "PC-101", machineName: "WIN-SILENT-02", publicKey: "другой-ключ");

        Assert.Equal(HttpStatusCode.OK, second.Response.StatusCode);
        Assert.Null(second.Body!.AssignedSeatName);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var holder = await db.DeviceSeatAssignments.SingleAsync(row => row.SeatId == TestIds.SeatId && row.DetachedAtUtc == null);
        Assert.Equal(first.Body!.DeviceId, holder.DeviceId);
    }

    // Переустановка того же ПК код не тратит и проходит даже по исчерпанному коду: иначе ПК со
    // слетевшей системой не вернуть в зал тем же скриптом.
    [Fact]
    public async Task ReinstallingTheSameMachine_SpendsNothing_AndKeepsItsSeat()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client, maxDevices: 1);
        var first = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");

        var again = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "нет-такого");

        Assert.Equal(HttpStatusCode.OK, again.Response.StatusCode);
        Assert.Equal(first.Body!.DeviceId, again.Body!.DeviceId);
        Assert.Equal("PC-101", again.Body.AssignedSeatName);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, (await db.InstallCodes.SingleAsync()).UsedDevices);
        Assert.Equal(1, await db.Devices.CountAsync(row => row.BranchId == TestIds.BranchId));
    }

    [Fact]
    public async Task UsedUpCode_RefusesANewMachine()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client, maxDevices: 1);
        await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");

        var another = await EnrollAsync(
            factory.CreateClient(), issued.Code!, seatName: null, machineName: "WIN-SILENT-02", publicKey: "другой-ключ");

        await AssertInvalidCodeAsync(another.Response);
        Assert.Empty(await ListAsync(client));
    }

    [Fact]
    public async Task ExpiredCode_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.InstallCodes.SingleAsync()).ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var enrolled = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");

        await AssertInvalidCodeAsync(enrolled.Response);
    }

    [Fact]
    public async Task RevokedCode_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client);

        var revoked = await client.DeleteAsync(
            InstallCodeRoutes.Single(TestIds.OrganizationId, TestIds.BranchId, issued.InstallCodeId));
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        var enrolled = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");
        await AssertInvalidCodeAsync(enrolled.Response);
    }

    [Theory]
    [InlineData("")]
    [InlineData("не код")]
    [InlineData("0000-0000-0000-0000")]
    public async Task SomethingThatIsNotAnIssuedCode_IsRefusedTheSameWay(string code)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var enrolled = await EnrollAsync(client, code, seatName: null);

        await AssertInvalidCodeAsync(enrolled.Response);
    }

    [Fact]
    public async Task BranchWithManualApproval_GetsTheMachineAsPending()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory, requireManualApproval: true);
        var issued = await IssueAsync(client);

        var enrolled = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");

        Assert.Equal(DeviceEnrollmentStateNames.Pending, enrolled.Body!.EnrollmentState);
    }

    [Fact]
    public async Task PlanAtItsDeviceLimit_RefusesANewMachine_AndSpendsNothing()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client);
        await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");
        await SetDeviceLimitAsync(factory, maxDevicesPerBranch: 1);

        var over = await EnrollAsync(
            factory.CreateClient(), issued.Code!, seatName: null, machineName: "WIN-SILENT-02", publicKey: "другой-ключ");

        Assert.Equal(HttpStatusCode.Conflict, over.Response.StatusCode);
        Assert.Equal(PlanLimitNames.ReachedCode, await ReadCodeAsync(over.Response));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, (await db.InstallCodes.SingleAsync()).UsedDevices);
    }

    // Мастер ставил ПК сверх тарифа: лимит проверял только старый вход по одноразовому коду.
    [Fact]
    public async Task Wizard_AtThePlanDeviceLimit_IsRefusedToo()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        await EnrollAsync(factory.CreateClient(), (await IssueAsync(client)).Code!, seatName: null);
        await SetDeviceLimitAsync(factory, maxDevicesPerBranch: 1);

        var response = await client.PostAsJsonAsync(
            InstallRoutes.AuthenticatedEnroll(TestIds.OrganizationId),
            new AuthenticatedInstallEnrollRequest(
                TestIds.BranchId, TestIds.SeatId, DeviceRoleNames.GamingPc, "Стенд 12", "WIN-WIZARD-01", "ключ-мастера"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PlanLimitNames.ReachedCode, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Operator_CannotIssueCodes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, MaxDevices: 10));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(24 * 7 + 1, 10)]
    [InlineData(24, 0)]
    [InlineData(24, 201)]
    public async Task CodeOutsideTheLimits_IsNotIssued(int lifetimeHours, int maxDevices)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);

        var response = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(lifetimeHours, maxDevices));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // В журнале — кто выдал код и каким он был по счёту, но не сам код: журнал читают многие.
    [Fact]
    public async Task Journal_NamesTheCodeById_NeverByItsValue()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        await SeedLayoutAsync(factory);
        var issued = await IssueAsync(client);

        var enrolled = await EnrollAsync(factory.CreateClient(), issued.Code!, seatName: "PC-101");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var records = await db.AuditRecords
            .Where(row => row.Action == AuditActionNames.CreateInstallCode || row.Action == AuditActionNames.InstallEnrollSucceeded)
            .ToListAsync();
        Assert.Equal(2, records.Count);
        var normalized = issued.Code!.Replace("-", string.Empty);
        Assert.All(records, record =>
        {
            Assert.DoesNotContain(issued.Code!, record.DetailsJson ?? string.Empty);
            Assert.DoesNotContain(normalized, record.DetailsJson ?? string.Empty);
        });
        var enrollRecord = Assert.Single(records, row => row.Action == AuditActionNames.InstallEnrollSucceeded);
        Assert.Equal(enrolled.Body!.DeviceId.ToString("D"), enrollRecord.TargetId);
        Assert.Contains(issued.InstallCodeId.ToString("D"), enrollRecord.DetailsJson);
    }

    private static async Task<InstallCodeDto> IssueAsync(HttpClient client, int maxDevices = 30)
    {
        var response = await client.PostAsJsonAsync(
            InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId),
            new CreateInstallCodeRequest(LifetimeHours: 24, maxDevices));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstallCodeDto>())!;
    }

    private static async Task<InstallCodeDto[]> ListAsync(HttpClient client)
    {
        var response = await client.GetAsync(InstallCodeRoutes.Branch(TestIds.OrganizationId, TestIds.BranchId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstallCodeDto[]>())!;
    }

    private static async Task<(HttpResponseMessage Response, InstallEnrollResponse? Body)> EnrollAsync(
        HttpClient client,
        string code,
        string? seatName,
        string machineName = "WIN-SILENT-01",
        string publicKey = PublicKey)
    {
        var response = await client.PostAsJsonAsync(
            InstallRoutes.CodeEnroll,
            new InstallCodeEnrollRequest(code, seatName, DisplayName: null, machineName, publicKey));
        var body = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<InstallEnrollResponse>()
            : null;
        return (response, body);
    }

    private static async Task AssertInvalidCodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(InstallErrorCodeNames.InstallCodeInvalid, await ReadCodeAsync(response));
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task SetDeviceLimitAsync(PlatformApiFactory factory, int maxDevicesPerBranch)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await db.Organizations.SingleAsync(row => row.OrganizationId == TestIds.OrganizationId);
        organization.LimitsJson = OrganizationLimitsJson.Serialize(
            new OrganizationLimitsDto(null, maxDevicesPerBranch, null, null));
        await db.SaveChangesAsync();
    }

    private static async Task SeedLayoutAsync(PlatformApiFactory factory, bool requireManualApproval = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var branch = await db.Branches.SingleAsync(row => row.BranchId == TestIds.BranchId);
        branch.RequireManualDeviceApproval = requireManualApproval;
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = TestIds.ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Общий зал",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-09-25T00:00:00Z")
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = TestIds.SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = TestIds.ZoneId,
            Name = "PC-101",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-09-25T00:00:00Z")
        });
        await db.SaveChangesAsync();
    }
}
