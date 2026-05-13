using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceCommandEndpointTests
{
    [Fact]
    public async Task PostDeviceCommand_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{TestIds.DeviceId}/commands",
            new
            {
                Type = "lock",
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceCommand_WithTechnicianPermission_ReturnsCommandAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedDeviceAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{TestIds.DeviceId}/commands",
            new
            {
                Type = "lock",
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var command = await response.Content.ReadFromJsonAsync<DeviceCommandDto>();

        Assert.NotNull(command);
        Assert.NotEqual(Guid.Empty, command.CommandId);
        Assert.Equal("lock", command.Type);
        Assert.Equal("operator-request", command.Payload["reason"]);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.DispatchDeviceCommand, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(command.CommandId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceCommand_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedDeviceAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{TestIds.DeviceId}/commands",
            new
            {
                Type = "lock",
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.DispatchDeviceCommand, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(TestIds.DeviceId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceCommand_PersistsPendingCommandStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedDeviceAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{TestIds.DeviceId}/commands",
            new
            {
                Type = "lock",
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
        });
        var command = await response.Content.ReadFromJsonAsync<DeviceCommandDto>();

        Assert.NotNull(command);

        var statusResponse = await client.GetAsync($"/api/devices/{TestIds.DeviceId}/commands/{command.CommandId}/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<DeviceCommandStatusDto>();

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.NotNull(status);
        Assert.Equal(TestIds.DeviceId, status.DeviceId);
        Assert.Equal(command.CommandId, status.CommandId);
        Assert.Equal("lock", status.Type);
        Assert.Equal("Pending", status.Status);
        Assert.Null(status.Message);
        Assert.Equal(command.CreatedAtUtc, status.CreatedAtUtc);
        Assert.Equal(command.CreatedAtUtc, status.UpdatedAtUtc);
    }

    [Fact]
    public async Task PostDeviceCommandResult_WithValidCredential_UpdatesPersistedCommandStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var enrollment = await EnrollDeviceAsync(client, factory);
        var commandId = Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94");
        await SeedPendingCommandAsync(factory, commandId, enrollment.DeviceId);
        var result = new DeviceCommandResultDto(
            enrollment.OrganizationId,
            enrollment.BranchId,
            enrollment.DeviceId,
            commandId,
            Status: "Accepted",
            Message: "handled from heartbeat fallback",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:02:00Z"));
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{enrollment.DeviceId:D}/commands/{commandId:D}/result")
        {
            Content = JsonContent.Create(result)
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, enrollment.CredentialSecret);

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var command = await dbContext.DeviceCommands.SingleAsync(candidate => candidate.CommandId == commandId);
        Assert.Equal("Accepted", command.Status);
        Assert.Equal("handled from heartbeat fallback", command.Message);
        Assert.Equal(result.ObservedAtUtc, command.UpdatedAtUtc);
    }

    [Fact]
    public async Task PostDeviceCommandResult_WithoutCredential_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var commandId = Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94");
        var result = new DeviceCommandResultDto(
            TestIds.OrganizationId,
            TestIds.BranchId,
            TestIds.DeviceId,
            commandId,
            Status: "Accepted",
            Message: "handled from heartbeat fallback",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:02:00Z"));

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{TestIds.DeviceId:D}/commands/{commandId:D}/result",
            result);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDeviceCommandStatus_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var commandId = Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94");

        var response = await client.GetAsync($"/api/devices/{TestIds.DeviceId}/commands/{commandId}/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDeviceCommandStatus_WithTechnicianPermission_ReturnsStatusAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var commandId = Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94");
        await SeedDeviceAsync(factory);
        await SeedPendingCommandAsync(factory, commandId);

        var response = await client.GetAsync($"/api/devices/{TestIds.DeviceId}/commands/{commandId}/status");
        var status = await response.Content.ReadFromJsonAsync<DeviceCommandStatusDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(status);
        Assert.Equal(commandId, status.CommandId);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewDeviceCommandStatus, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(commandId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task GetDeviceCommandStatus_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var commandId = Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94");
        await SeedDeviceAsync(factory);
        await SeedPendingCommandAsync(factory, commandId);

        var response = await client.GetAsync($"/api/devices/{TestIds.DeviceId}/commands/{commandId}/status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewDeviceCommandStatus, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(commandId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task GetDeviceCommandStatus_ReturnsNotFoundForUnknownCommand()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedDeviceAsync(factory);

        var commandId = Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94");

        var response = await client.GetAsync($"/api/devices/{TestIds.DeviceId}/commands/{commandId}/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostDeviceCommand_ReturnsBadRequestForBlankCommandType(string commandType)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedDeviceAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{TestIds.DeviceId}/commands",
            new
            {
                Type = commandType,
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceCommand_ReturnsBadRequestForMissingPayload()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedDeviceAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/devices/{TestIds.DeviceId}/commands",
            new
            {
                Type = "lock",
                Payload = (Dictionary<string, string>?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task SeedDeviceAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Devices.Add(new DeviceEntity
        {
            DeviceId = TestIds.DeviceId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            EnrolledAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(
        HttpClient client,
        PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var code = "AFK4-TEST-COMMANDRESULT";
        dbContext.DeviceEnrollmentCodes.Add(new DeviceEnrollmentCodeEntity
        {
            Code = code,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            ExpiresAtUtc = DateTimeOffset.Parse("2026-06-12T01:00:00Z")
        });
        await dbContext.SaveChangesAsync();

        var response = await client.PostAsJsonAsync(
            "/api/devices/enroll",
            new DeviceEnrollmentRequest(
                TestIds.OrganizationId,
                TestIds.BranchId,
                code,
                MachineName: "PC-001",
                AgentVersion: "0.1.0",
                ShellVersion: "0.1.0",
                RequestedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z")));
        var body = await response.Content.ReadFromJsonAsync<DeviceEnrollmentResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        return body;
    }

    private static async Task SeedPendingCommandAsync(
        PlatformApiFactory factory,
        Guid commandId,
        Guid? deviceId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var createdAt = DateTimeOffset.Parse("2026-05-12T00:01:00Z");
        dbContext.DeviceCommands.Add(new DeviceCommandEntity
        {
            DeviceId = deviceId ?? TestIds.DeviceId,
            CommandId = commandId,
            Type = "lock",
            PayloadJson = """{"reason":"operator-request"}""",
            Status = "Pending",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt
        });
        await dbContext.SaveChangesAsync();
    }
}
