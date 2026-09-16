using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// «Позвать оператора» с игрового ПК. Зовёт агент ключом устройства: на запертом экране сессии
/// нет, и назвать себя игроку нечем — а машина известна всегда.
/// </summary>
public sealed class DeviceAssistanceRequestEndpointTests
{
    [Fact]
    public async Task PostAssistanceRequest_WithValidDeviceCredential_RaisesTheCallOnTheSeat()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        var response = await CallOperatorAsync(client, enrollment, DateTimeOffset.Parse("2026-09-16T10:00:00Z"));
        var state = await response.Content.ReadFromJsonAsync<DeviceAssistanceStateDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DateTimeOffset.Parse("2026-09-16T10:00:00Z"), state!.AssistanceRequestedAtUtc);
    }

    // Стойке важно, сколько человек уже ждёт: десятый тычок в кнопку не должен обнулять ожидание.
    [Fact]
    public async Task PostAssistanceRequest_PressedAgain_KeepsTheFirstWaitingTime()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var first = DateTimeOffset.Parse("2026-09-16T10:00:00Z");

        await CallOperatorAsync(client, enrollment, first);
        var second = await CallOperatorAsync(client, enrollment, first.AddMinutes(4));
        var state = await second.Content.ReadFromJsonAsync<DeviceAssistanceStateDto>();

        Assert.Equal(first, state!.AssistanceRequestedAtUtc);
    }

    [Fact]
    public async Task PostAssistanceRequest_WithoutTheDeviceCredential_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{enrollment.DeviceId}/assistance-request")
        {
            Content = JsonContent.Create(new DeviceAssistanceRequest(
                enrollment.OrganizationId,
                enrollment.BranchId,
                enrollment.DeviceId,
                DateTimeOffset.Parse("2026-09-16T10:00:00Z")))
        };
        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Ради этого всё и делается: вызов видно на карте зала, вместе со временем ожидания.
    [Fact]
    public async Task GetFloorMap_ShowsTheSeatThatIsCallingAndForgetsItOnceResolved()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var enrollment = await EnrollDeviceAsync(client);
        await AssignDeviceToSeatAsync(factory, enrollment.DeviceId);
        await CallOperatorAsync(client, enrollment, DateTimeOffset.Parse("2026-09-16T10:00:00Z"));

        var calling = await ReadSeatAsync(client);
        Assert.Equal(DateTimeOffset.Parse("2026-09-16T10:00:00Z"), calling.AssistanceRequestedAtUtc);

        var resolved = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{enrollment.DeviceId:D}/assistance-request/resolve",
            new DeviceStateChangeRequest(TestIds.OrganizationId, "Подошёл к месту."));
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);

        var quiet = await ReadSeatAsync(client);
        Assert.Null(quiet.AssistanceRequestedAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .SingleAsync(record => record.Action == AuditActionNames.ResolveAssistanceRequest);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    // Снять вызов — работа зала. У техника этого права нет, и отказ остаётся в журнале.
    [Fact]
    public async Task ResolveAssistanceRequest_WithoutThePermission_IsForbiddenAndAudited()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        await CallOperatorAsync(client, enrollment, DateTimeOffset.Parse("2026-09-16T10:00:00Z"));

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{enrollment.DeviceId:D}/assistance-request/resolve",
            new DeviceStateChangeRequest(TestIds.OrganizationId, "Нет прав."));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords
            .SingleAsync(record => record.Action == AuditActionNames.ResolveAssistanceRequest);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    private static Task<HttpResponseMessage> CallOperatorAsync(
        HttpClient client,
        DeviceEnrollmentResponse enrollment,
        DateTimeOffset requestedAtUtc)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{enrollment.DeviceId}/assistance-request")
        {
            Content = JsonContent.Create(new DeviceAssistanceRequest(
                enrollment.OrganizationId,
                enrollment.BranchId,
                enrollment.DeviceId,
                requestedAtUtc))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);
        return client.SendAsync(message);
    }

    private static async Task<SeatStatusDto> ReadSeatAsync(HttpClient client)
    {
        var floorMap = await client.GetFromJsonAsync<FloorMapDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/floor-map");
        return floorMap!.Seats.Single();
    }

    private static async Task AssignDeviceToSeatAsync(PlatformApiFactory factory, Guid deviceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.Parse("2026-09-16T09:00:00Z");
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Зал A",
            SortOrder = 1,
            CreatedAtUtc = now
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = zoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = now
        });
        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            DeviceId = deviceId,
            SeatId = seatId,
            AttachedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(HttpClient client)
    {
        var codeResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();
        Assert.NotNull(code);

        var enrollmentResponse = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                OrganizationId: TestIds.OrganizationId,
                BranchId: TestIds.BranchId,
                EnrollmentCode: code!.Code,
                MachineName: "PC-001",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: DateTimeOffset.Parse("2026-09-16T09:55:00Z")));
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();
        Assert.NotNull(enrollment);
        return enrollment!;
    }
}
