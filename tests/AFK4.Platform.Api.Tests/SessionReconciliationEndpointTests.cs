using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
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
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
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
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
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
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
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
    public async Task SessionReconciliation_WithCloudActiveSessionAndNoLocalLease_ReturnsUnlockAndSignedLease()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);
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

    private static async Task<SessionReconciliationResponse> PostReconciliationAsync(
        HttpClient client,
        DeviceEnrollmentResponse enrollment,
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

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(HttpClient client)
    {
        var codeResponse = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));
        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);
        var code = await codeResponse.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();
        Assert.NotNull(code);

        var enrollmentResponse = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                OrganizationId: TestIds.OrganizationId,
                BranchId: TestIds.BranchId,
                EnrollmentCode: code.Code,
                MachineName: "PC-001",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: Now.AddMinutes(1)));
        Assert.Equal(HttpStatusCode.OK, enrollmentResponse.StatusCode);
        var enrollment = await enrollmentResponse.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();
        Assert.NotNull(enrollment);

        return enrollment;
    }

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
            $"/api/branches/{TestIds.BranchId:D}/sessions/start",
            new StartGuestSessionRequest(
                OrganizationId: TestIds.OrganizationId,
                SeatId: SeatId,
                DurationMinutes: 60,
                TariffRuleVersionId: "manual-v1",
                IdempotencyKey: $"start-seat-1-{Guid.NewGuid():N}"));
        var body = await response.Content.ReadFromJsonAsync<SessionCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotNull(body.Session.CurrentLease);

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
