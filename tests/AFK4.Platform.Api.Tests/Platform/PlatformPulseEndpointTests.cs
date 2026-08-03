using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Platform.Pulse;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformPulseEndpointTests
{
    private static CreateOrganizationRequest BuildCreateOrganizationRequest(
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch")
    {
        return new CreateOrganizationRequest(
            OrganizationSlug: orgSlug,
            OrganizationName: "Demo Club",
            BranchSlug: branchSlug,
            BranchName: "Demo Branch",
            BranchCity: "Dushanbe",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new OrganizationLimitsDto(3, 60, 80, 20),
            OwnerUserName: null,
            OwnerDisplayName: null,
            OrganizationOwnerInviteLifetime: null);
    }

    private static async Task<(Guid OrganizationId, Guid BranchId)> CreateOrganizationAsync(
        HttpClient client,
        string orgSlug = "demo-club",
        string branchSlug = "demo-branch")
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            BuildCreateOrganizationRequest(orgSlug, branchSlug));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return (body.Organization.OrganizationId, body.Organization.Branches[0].BranchId);
    }

    [Fact]
    public async Task GetPulse_OrganizationWithSilentAgent_ReturnsCriticalAlert()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.Devices.Add(new DeviceEntity
            {
                DeviceId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = branchId,
                MachineName = "PC-01",
                DisplayName = "PC-01",
                IsOnline = false,
                LastHeartbeatAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                EnrolledAtUtc = DateTimeOffset.UtcNow.AddDays(-10)
            });
            await dbContext.SaveChangesAsync();
        }

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        var club = Assert.Single(organization.Clubs);
        Assert.Equal(0, club.DevicesOnline);
        Assert.Equal(1, club.DevicesTotal);
        Assert.Contains(club.Alerts, alert => alert.Kind == PulseAlertKindNames.AgentSilent);
        Assert.Equal(PulseAlertLevelNames.Critical, organization.AlertLevel);
    }

    [Fact]
    public async Task GetPulse_ClubWithoutDevices_DoesNotRaiseAgentSilentAlert()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        await CreateOrganizationAsync(client);

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        var club = Assert.Single(organization.Clubs);
        Assert.Equal(0, club.DevicesTotal);
        Assert.DoesNotContain(club.Alerts, alert => alert.Kind == PulseAlertKindNames.AgentSilent);
        Assert.Equal(PulseAlertLevelNames.Normal, organization.AlertLevel);
    }

    [Fact]
    public async Task GetPulse_OpenShiftPastStaleThreshold_ReturnsAttentionAlertOnClub()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.Shifts.Add(new ShiftEntity
            {
                ShiftId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = branchId,
                OpenedByStaffUserId = Guid.NewGuid(),
                State = "open",
                CurrencyCode = "TJS",
                OpenedAtUtc = DateTimeOffset.UtcNow.AddHours(-30)
            });
            await dbContext.SaveChangesAsync();
        }

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        var club = Assert.Single(organization.Clubs);
        Assert.True(club.ShiftOpen);
        Assert.Contains(club.Alerts, alert => alert.Kind == PulseAlertKindNames.ShiftNotClosed);
        Assert.Equal(PulseAlertLevelNames.Attention, organization.AlertLevel);
    }

    [Fact]
    public async Task GetPulse_OverdueInvoice_RaisesOrganizationLevelAlert()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, _) = await CreateOrganizationAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.Invoices.Add(new InvoiceEntity
            {
                InvoiceId = Guid.NewGuid(),
                OrganizationId = organizationId,
                Number = 1,
                Kind = "subscription",
                PeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-60),
                PeriodEndUtc = DateTimeOffset.UtcNow.AddDays(-30),
                IssuedAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
                DueAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
                AmountMinorUnits = 50_000,
                CurrencyCode = "TJS",
                Status = InvoiceStatusNames.Overdue,
                Description = "Subscription",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-30),
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-30)
            });
            await dbContext.SaveChangesAsync();
        }

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        Assert.Contains(organization.Alerts, alert => alert.Kind == PulseAlertKindNames.PaymentOverdue);
        Assert.Equal(50_000, organization.OutstandingMinorUnits);
        Assert.Equal(PulseAlertLevelNames.Attention, organization.AlertLevel);
    }

    [Fact]
    public async Task GetPulse_OneOfTwoClubsSilent_OrganizationEscalatesToCriticalOnlyForSilentClub()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, healthyBranchId) = await CreateOrganizationAsync(client);
        var silentBranchId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.Branches.Add(new BranchEntity
            {
                BranchId = silentBranchId,
                OrganizationId = organizationId,
                Slug = "second-branch",
                Name = "Second Branch",
                City = "Khujand",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            dbContext.Devices.Add(new DeviceEntity
            {
                DeviceId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = healthyBranchId,
                MachineName = "PC-HEALTHY",
                DisplayName = "PC-HEALTHY",
                IsOnline = true,
                LastHeartbeatAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                EnrolledAtUtc = DateTimeOffset.UtcNow.AddDays(-10)
            });
            dbContext.Devices.Add(new DeviceEntity
            {
                DeviceId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = silentBranchId,
                MachineName = "PC-SILENT",
                DisplayName = "PC-SILENT",
                IsOnline = false,
                LastHeartbeatAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                EnrolledAtUtc = DateTimeOffset.UtcNow.AddDays(-10)
            });
            await dbContext.SaveChangesAsync();
        }

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        Assert.Equal(2, organization.Clubs.Count);
        Assert.Equal(PulseAlertLevelNames.Critical, organization.AlertLevel);

        var healthyClub = organization.Clubs.Single(club => club.BranchId == healthyBranchId);
        var silentClub = organization.Clubs.Single(club => club.BranchId == silentBranchId);
        Assert.Empty(healthyClub.Alerts);
        Assert.Contains(silentClub.Alerts, alert => alert.Kind == PulseAlertKindNames.AgentSilent);
    }

    [Fact]
    public async Task GetPulse_WorstClubAlertIsAttention_OrganizationLevelIsAttentionNotCritical()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, healthyBranchId) = await CreateOrganizationAsync(client);
        var staleShiftBranchId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.Branches.Add(new BranchEntity
            {
                BranchId = staleShiftBranchId,
                OrganizationId = organizationId,
                Slug = "stale-shift-branch",
                Name = "Stale Shift Branch",
                City = "Khujand",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            dbContext.Shifts.Add(new ShiftEntity
            {
                ShiftId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = staleShiftBranchId,
                OpenedByStaffUserId = Guid.NewGuid(),
                State = "open",
                CurrencyCode = "TJS",
                OpenedAtUtc = DateTimeOffset.UtcNow.AddHours(-30)
            });
            await dbContext.SaveChangesAsync();
        }

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        Assert.Equal(2, organization.Clubs.Count);
        Assert.Equal(PulseAlertLevelNames.Attention, organization.AlertLevel);

        var healthyClub = organization.Clubs.Single(club => club.BranchId == healthyBranchId);
        var staleShiftClub = organization.Clubs.Single(club => club.BranchId == staleShiftBranchId);
        Assert.Empty(healthyClub.Alerts);
        Assert.Contains(staleShiftClub.Alerts, alert => alert.Kind == PulseAlertKindNames.ShiftNotClosed);
    }

    [Fact]
    public async Task GetPulse_RolloutInRollbackRequestedState_RaisesOrganizationLevelAlert()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var package = new UpdatePackageEntity
            {
                UpdatePackageId = Guid.NewGuid(),
                Component = "agent-service",
                Version = "1.2.3",
                Channel = "stable",
                ArtifactUri = "https://updates.afk4.net/agent-service-1.2.3.zip",
                Sha256 = new string('a', 64),
                Signature = "signature",
                SignatureAlgorithm = "ecdsa-p256-sha256-ieee-p1363",
                SizeBytes = 1024,
                State = "validated",
                ReleaseNotes = "notes",
                CreatedByPlatformAdminUserId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5)
            };
            dbContext.UpdatePackages.Add(package);

            var rollout = new UpdateRolloutEntity
            {
                UpdateRolloutId = Guid.NewGuid(),
                UpdatePackageId = package.UpdatePackageId,
                Component = package.Component,
                Version = package.Version,
                Channel = package.Channel,
                State = UpdateRolloutStateNames.RollbackRequested,
                TargetKind = PlatformUpdateTargetKindNames.Branch,
                BatchPercent = 100,
                Reason = "rollout failed on canary devices",
                CreatedByPlatformAdminUserId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            };
            dbContext.UpdateRollouts.Add(rollout);
            dbContext.UpdateRolloutTargets.Add(new UpdateRolloutTargetEntity
            {
                UpdateRolloutTargetId = Guid.NewGuid(),
                UpdateRolloutId = rollout.UpdateRolloutId,
                TargetKind = PlatformUpdateTargetKindNames.Branch,
                BranchId = branchId,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await dbContext.SaveChangesAsync();
        }

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        Assert.Contains(organization.Alerts, alert => alert.Kind == PulseAlertKindNames.RolloutFailed);
        Assert.Equal(PulseAlertLevelNames.Attention, organization.AlertLevel);
    }

    [Fact]
    public async Task GetPulse_RolloutCompleted_DoesNotRaiseRolloutFailedAlert()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var (organizationId, branchId) = await CreateOrganizationAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var package = new UpdatePackageEntity
            {
                UpdatePackageId = Guid.NewGuid(),
                Component = "agent-service",
                Version = "1.2.3",
                Channel = "stable",
                ArtifactUri = "https://updates.afk4.net/agent-service-1.2.3.zip",
                Sha256 = new string('a', 64),
                Signature = "signature",
                SignatureAlgorithm = "ecdsa-p256-sha256-ieee-p1363",
                SizeBytes = 1024,
                State = "validated",
                ReleaseNotes = "notes",
                CreatedByPlatformAdminUserId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5)
            };
            dbContext.UpdatePackages.Add(package);

            var rollout = new UpdateRolloutEntity
            {
                UpdateRolloutId = Guid.NewGuid(),
                UpdatePackageId = package.UpdatePackageId,
                Component = package.Component,
                Version = package.Version,
                Channel = package.Channel,
                State = UpdateRolloutStateNames.Completed,
                TargetKind = PlatformUpdateTargetKindNames.Branch,
                BatchPercent = 100,
                Reason = "completed successfully",
                CreatedByPlatformAdminUserId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                StartsAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.UpdateRollouts.Add(rollout);
            dbContext.UpdateRolloutTargets.Add(new UpdateRolloutTargetEntity
            {
                UpdateRolloutTargetId = Guid.NewGuid(),
                UpdateRolloutId = rollout.UpdateRolloutId,
                TargetKind = PlatformUpdateTargetKindNames.Branch,
                BranchId = branchId,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await dbContext.SaveChangesAsync();
        }

        var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

        Assert.NotNull(pulse);
        var organization = Assert.Single(pulse.Organizations);
        Assert.DoesNotContain(organization.Alerts, alert => alert.Kind == PulseAlertKindNames.RolloutFailed);
        Assert.Equal(PulseAlertLevelNames.Normal, organization.AlertLevel);
    }

    [Fact]
    public async Task GetPulse_WithoutAuth_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/pulse");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPulse_WithoutPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync("/api/platform/pulse");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
