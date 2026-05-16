using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class BranchDiagnosticsEndpointTests
{
    [Fact]
    public async Task GetDiagnostics_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/diagnostics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDiagnostics_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/diagnostics");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewDiagnostics, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task GetDiagnostics_WithTechnicianRole_ReturnsDiagnosticsAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        await SeedDiagnosticsDataAsync(factory);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/diagnostics");
        var result = await response.Content.ReadFromJsonAsync<BranchDiagnosticsDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(2, result.DeviceSummary.TotalDevices);
        Assert.Equal(1, result.CommandSummary.PendingCommands);
        Assert.Equal(1, result.UpdateSummary.FailedDevices);
        Assert.DoesNotContain(result.StaleDevices, device => device.MachineName == "PC-OTHER");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewDiagnostics, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task GetDiagnostics_WithAuditorRole_ReturnsOk()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.AccountantAuditor);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task SeedDiagnosticsDataAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow;
        var staleDeviceId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var otherBranchId = Guid.Parse("99999999-9999-4999-8999-999999999999");
        var otherBranchDeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var rolloutId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var packageId = Guid.Parse("55555555-5555-4555-8555-555555555555");

        dbContext.Devices.AddRange(
            new DeviceEntity
            {
                DeviceId = TestIds.DeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-001",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = now.AddDays(-1),
                LastHeartbeatAtUtc = now.AddMinutes(-1),
                IsOnline = true,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = staleDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-STALE",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = now.AddDays(-1),
                LastHeartbeatAtUtc = now.AddHours(-1),
                IsOnline = false,
                IsLocked = true
            },
            new DeviceEntity
            {
                DeviceId = otherBranchDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = otherBranchId,
                MachineName = "PC-OTHER",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = now.AddDays(-1),
                LastHeartbeatAtUtc = now.AddHours(-1),
                IsOnline = false,
                IsLocked = true
            });

        dbContext.DeviceCommands.AddRange(
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("66666666-6666-4666-8666-666666666666"),
                DeviceId = TestIds.DeviceId,
                Type = "lock",
                PayloadJson = "{}",
                Status = "Pending",
                CreatedAtUtc = now.AddMinutes(-2),
                UpdatedAtUtc = now.AddMinutes(-2)
            },
            new DeviceCommandEntity
            {
                CommandId = Guid.Parse("77777777-7777-4777-8777-777777777777"),
                DeviceId = otherBranchDeviceId,
                Type = "lock",
                PayloadJson = "{}",
                Status = "Failed",
                CreatedAtUtc = now.AddMinutes(-2),
                UpdatedAtUtc = now.AddMinutes(-2)
            });

        dbContext.UpdateRollouts.Add(new UpdateRolloutEntity
        {
            UpdateRolloutId = rolloutId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            UpdatePackageId = packageId,
            Component = "gaming-pc",
            Version = "0.1.0",
            Channel = "internal",
            State = "active",
            TargetKind = "branch",
            BatchPercent = 100,
            Reason = "test",
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            CreatedAtUtc = now.AddMinutes(-10),
            StartsAtUtc = now.AddMinutes(-10)
        });

        dbContext.DeviceUpdateStatuses.Add(new DeviceUpdateStatusEntity
        {
            DeviceUpdateStatusId = Guid.Parse("88888888-8888-4888-8888-888888888888"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            DeviceId = staleDeviceId,
            UpdateRolloutId = rolloutId,
            UpdatePackageId = packageId,
            Component = "gaming-pc",
            InstalledVersion = "0.0.9",
            TargetVersion = "0.1.0",
            Status = "failed",
            Message = "install failed",
            FirstReportedAtUtc = now.AddMinutes(-5),
            UpdatedAtUtc = now.AddMinutes(-4)
        });

        await dbContext.SaveChangesAsync();
    }
}
