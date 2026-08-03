using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Platform.Pulse;
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
