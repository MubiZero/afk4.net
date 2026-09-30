using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Tests.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionReconciliationEndpointTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task SessionReconciliation_WithoutDeviceCredential_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Technician);
        var enrollment = await EnrollDeviceAsync(client);
        var request = CreateSnapshot(enrollment.DeviceId, activeLease: null);

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{enrollment.DeviceId:D}/session-reconciliation",
            request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SessionReconciliation_WithMatchingCloudAndLocalLease_ReturnsContinue()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedSeatAssignmentAsync(factory, enrollment.DeviceId);
        var started = await StartSessionAsync(client);

        var body = await PostReconciliationAsync(
            client,
            enrollment,
            CreateSnapshot(enrollment.DeviceId, started.Session.CurrentLease));

        Assert.Equal("continue", body.Action);
        Assert.Equal(started.Session.SessionId, body.SessionId);
        Assert.Null(body.Lease);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.DeviceCommands.Where(command => command.DeviceId == enrollment.DeviceId).ToListAsync());
        Assert.Contains(
            await dbContext.SessionEvents.Where(sessionEvent => sessionEvent.SessionId == started.Session.SessionId).ToListAsync(),
            sessionEvent => sessionEvent.EventType == "device-reconciled");
    }

    [Fact]
    public async Task SessionReconciliation_WithLocalLeaseForCloudEndedSession_ReturnsLock()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedSeatAssignmentAsync(factory, enrollment.DeviceId);
        var started = await StartSessionAsync(client);
        Assert.NotNull(started.Session.CurrentLease);
        await MarkSessionEndedAsync(factory, started.Session.SessionId);

        var body = await PostReconciliationAsync(
            client,
            enrollment,
            CreateSnapshot(enrollment.DeviceId, started.Session.CurrentLease));

        Assert.Equal("lock", body.Action);
        Assert.Equal(started.Session.SessionId, body.SessionId);
        Assert.Null(body.Lease);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var lockCommand = await dbContext.DeviceCommands
            .Where(command => command.DeviceId == enrollment.DeviceId && command.Type == "lock")
            .SingleAsync();
        Assert.Contains(started.Session.SessionId.ToString("D"), lockCommand.PayloadJson);
        Assert.Contains(
            await dbContext.SessionEvents.Where(sessionEvent => sessionEvent.SessionId == started.Session.SessionId).ToListAsync(),
            sessionEvent => sessionEvent.EventType == "device-reconciled");
    }

    [Fact]
    public async Task SessionReconciliation_WithEndingCloudSessionAndExistingLock_DoesNotDispatchDuplicateLock()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedSeatAssignmentAsync(factory, enrollment.DeviceId);
        var started = await StartSessionAsync(client);
        var ending = await EndSessionAsync(client, started.Session.SessionId);
        Assert.Single(ending.DeviceCommands);

        var body = await PostReconciliationAsync(
            client,
            enrollment,
            CreateSnapshot(enrollment.DeviceId, started.Session.CurrentLease));

        Assert.Equal("lock", body.Action);
        Assert.Equal(started.Session.SessionId, body.SessionId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var lockCommands = await dbContext.DeviceCommands
            .Where(command => command.DeviceId == enrollment.DeviceId && command.Type == "lock")
            .ToListAsync();
        var lockCommand = Assert.Single(lockCommands);
        Assert.Contains(started.Session.SessionId.ToString("D"), lockCommand.PayloadJson);
        Assert.Contains(
            await dbContext.SessionEvents.Where(sessionEvent => sessionEvent.SessionId == started.Session.SessionId).ToListAsync(),
            sessionEvent => sessionEvent.EventType == "device-reconciled");
    }

    [Fact]
    public async Task SessionReconciliation_WithCloudActiveSessionAndNoLocalLease_ReturnsUnlockAndSignedLease()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedSeatAssignmentAsync(factory, enrollment.DeviceId);
        var started = await StartSessionAsync(client);

        var body = await PostReconciliationAsync(
            client,
            enrollment,
            CreateSnapshot(enrollment.DeviceId, activeLease: null));

        Assert.Equal("unlock", body.Action);
        Assert.Equal(started.Session.SessionId, body.SessionId);
        Assert.NotNull(body.Lease);
        Assert.False(string.IsNullOrWhiteSpace(body.Lease.Signature));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var unlockCommands = await dbContext.DeviceCommands
            .Where(command => command.DeviceId == enrollment.DeviceId && command.Type == "unlock")
            .ToListAsync();
        Assert.Equal(2, unlockCommands.Count);
        Assert.Contains("\"sessionLease\"", unlockCommands[^1].PayloadJson);
        Assert.Contains(
            await dbContext.SessionEvents.Where(sessionEvent => sessionEvent.SessionId == started.Session.SessionId).ToListAsync(),
            sessionEvent => sessionEvent.EventType == "device-reconciled");
    }

    [Fact]
    public async Task SessionReconciliation_WithPausedCloudSessionAndNoLocalLease_DoesNotUnlockThePc()
    {
        // Приёмка 30.09.2026: сверка отпирала ПК на паузе так же, как сердцебиение.
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedSeatAssignmentAsync(factory, enrollment.DeviceId);
        var started = await StartSessionAsync(client);
        var pause = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/sessions/{started.Session.SessionId:D}/pause",
            new PauseSessionRequest("Отошёл", $"pause-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);

        var body = await PostReconciliationAsync(
            client,
            enrollment,
            CreateSnapshot(enrollment.DeviceId, activeLease: null));

        Assert.Equal("continue", body.Action);
        Assert.Null(body.Lease);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.DeviceCommands
            .Where(command => command.DeviceId == enrollment.DeviceId && command.Type == "unlock")
            .ToListAsync());
    }

    [Fact]
    public async Task SessionReconciliation_WithALockTheOperatorSetMidSession_DoesNotUnlockThePc()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedSeatAssignmentAsync(factory, enrollment.DeviceId);
        await StartSessionAsync(client);
        var locked = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{enrollment.DeviceId:D}/commands",
            new CreateDeviceCommandRequest(
                "lock",
                new Dictionary<string, string> { ["reason"] = "operator-pc-control", ["source"] = "operator-map" }));
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);

        var body = await PostReconciliationAsync(
            client,
            enrollment,
            CreateSnapshot(enrollment.DeviceId, activeLease: null));

        Assert.Equal("continue", body.Action);
        Assert.Null(body.Lease);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await dbContext.DeviceCommands
            .Where(command => command.DeviceId == enrollment.DeviceId && command.Type == "unlock")
            .ToListAsync());
    }

    [Fact]
    public async Task OperatorUnlockMidSession_CarriesTheLeaseAndReleasesTheHeldLock()
    {
        // Без аренды агент отвечает «sessionLease обязателен» и не отпирает: блокировка оператора,
        // которая держится до его unlock, оказалась бы вечной.
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);
        await SeedSeatAssignmentAsync(factory, enrollment.DeviceId);
        var started = await StartSessionAsync(client);
        await PostOperatorCommandAsync(client, enrollment.DeviceId, "lock");

        var unlock = await PostOperatorCommandAsync(client, enrollment.DeviceId, "unlock");

        Assert.Equal(started.Session.SessionId.ToString("D"), unlock.Payload["sessionId"]);
        var lease = System.Text.Json.JsonSerializer.Deserialize<SessionLeaseDto>(
            unlock.Payload["sessionLease"],
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.NotNull(lease);
        Assert.Equal(started.Session.SessionId, lease.SessionId);
        Assert.True(lease.Sequence > started.Session.CurrentLease!.Sequence);
        Assert.Equal("operator-pc-control", unlock.Payload["reason"]);

        var body = await PostReconciliationAsync(
            client,
            enrollment,
            CreateSnapshot(enrollment.DeviceId, activeLease: null));
        Assert.Equal("unlock", body.Action);
    }

    [Fact]
    public async Task OperatorUnlockOnAFreePc_GoesThroughAsItIs()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        var enrollment = await EnrollDeviceAsync(client);

        var unlock = await PostOperatorCommandAsync(client, enrollment.DeviceId, "unlock");

        Assert.DoesNotContain("sessionLease", unlock.Payload.Keys);
    }

    private static async Task<DeviceCommandDto> PostOperatorCommandAsync(HttpClient client, Guid deviceId, string type)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/devices/{deviceId:D}/commands",
            new CreateDeviceCommandRequest(
                type,
                new Dictionary<string, string> { ["reason"] = "operator-pc-control", ["source"] = "operator-map" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<DeviceCommandDto>())!;
    }

    private static async Task<SessionReconciliationResponse> PostReconciliationAsync(
        HttpClient client,
        InstallEnrollResponse enrollment,
        DeviceSessionSnapshotRequest request)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{enrollment.DeviceId:D}/session-reconciliation")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);

        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SessionReconciliationResponse>();
        Assert.NotNull(body);

        return body;
    }

    private static DeviceSessionSnapshotRequest CreateSnapshot(Guid deviceId, SessionLeaseDto? activeLease)
    {
        return new DeviceSessionSnapshotRequest(
            OrganizationId: TestIds.OrganizationId,
            BranchId: TestIds.BranchId,
            DeviceId: deviceId,
            ActiveSessionId: activeLease?.SessionId,
            ActiveLease: activeLease,
            IsLocked: activeLease is null,
            PendingLocalEventCount: 0,
            ObservedAtUtc: Now.AddMinutes(2));
    }

    private static Task<InstallEnrollResponse> EnrollDeviceAsync(HttpClient client) =>
        TestDeviceEnrollment.EnrollDeviceAsync(client, TestIds.OrganizationId, TestIds.BranchId);

    private static async Task SeedSeatAssignmentAsync(PlatformApiFactory factory, Guid deviceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = ZoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        dbContext.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = deviceId,
            AttachedAtUtc = Now
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<SessionCommandResponse> StartSessionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(
                OrganizationId: TestIds.OrganizationId,
                SeatId: SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "manual-v1",
                IdempotencyKey: $"start-seat-1-{Guid.NewGuid():N}"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotNull(body.Session.CurrentLease);

        return body;
    }

    private static async Task<SessionCommandResponse> EndSessionAsync(HttpClient client, Guid sessionId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/sessions/{sessionId:D}/end",
            new EndSessionRequest("operator-end", $"end-seat-1-{Guid.NewGuid():N}"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(SessionStateNames.Ending, body.Session.State);

        return body;
    }

    private static async Task MarkSessionEndedAsync(PlatformApiFactory factory, Guid sessionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await dbContext.Sessions.SingleAsync(candidate => candidate.SessionId == sessionId);
        session.State = SessionStateNames.Ended;
        session.EndedAtUtc = Now.AddMinutes(1);
        session.UpdatedAtUtc = Now.AddMinutes(1);
        await dbContext.SaveChangesAsync();
    }
}
