using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Invites;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerInviteEmailEndpointTests
{
    private static CreateTenantRequest BuildCreateTenantRequest(string slug) => new(
        OrganizationSlug: slug,
        OrganizationName: "Demo Club",
        BranchSlug: $"{slug}-branch",
        BranchName: "Demo Branch",
        BranchCity: "Dushanbe",
        PlanCode: TenantPlanCodeNames.Starter,
        SubscriptionStatus: SubscriptionStatusNames.Trial,
        Limits: new TenantLimitsDto(MaxBranches: 3, MaxDevicesPerBranch: 60, MaxConcurrentSessions: 80, MaxStaffUsersPerBranch: 20),
        OwnerUserName: "owner@demo.test",
        OwnerDisplayName: "Demo Owner",
        OwnerInviteLifetime: TimeSpan.FromDays(7));

    private static async Task<(Guid OrganizationId, Guid BranchId)> CreateTenantAsync(PlatformApiFactory factory, HttpClient client, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest(slug));
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        return (body!.Tenant.OrganizationId, body.Tenant.Branches[0].BranchId);
    }

    private static async Task<int> OwnerInviteEmailCountAsync(PlatformApiFactory factory, string toAddress)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.NotificationOutbox
            .CountAsync(row => row.TemplateKey == NotificationTemplateKeys.OwnerInvite && row.RecipientAddress == toAddress);
    }

    [Fact]
    public async Task CreateOwnerInvite_WithEmail_EnqueuesOwnerInviteEmailCarryingTheCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateTenantAsync(factory, client, "club-a");

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/owner-invites",
            new CreateOwnerInviteRequest(branchId, "newowner", "New Owner", null, "newowner@club.example"));
        var invite = await response.Content.ReadFromJsonAsync<OwnerInviteDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await OwnerInviteEmailCountAsync(factory, "newowner@club.example"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.NotificationOutbox.SingleAsync(r => r.TemplateKey == NotificationTemplateKeys.OwnerInvite);
        Assert.Contains(invite!.Code, row.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateOwnerInvite_WithoutEmail_DoesNotEnqueueEmail()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateTenantAsync(factory, client, "club-b");

        var response = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/owner-invites",
            new CreateOwnerInviteRequest(branchId, "newowner", "New Owner", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await db.NotificationOutbox.CountAsync(r => r.TemplateKey == NotificationTemplateKeys.OwnerInvite));
    }

    [Fact]
    public async Task ResendOwnerInvite_EnqueuesAnotherEmail()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateTenantAsync(factory, client, "club-c");
        var createResponse = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/owner-invites",
            new CreateOwnerInviteRequest(branchId, "newowner", "New Owner", null, "newowner@club.example"));
        var invite = await createResponse.Content.ReadFromJsonAsync<OwnerInviteDto>();

        var resend = await client.PostAsync($"/api/platform/owner-invites/{invite!.OwnerInviteId:D}/resend", null);

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Equal(2, await OwnerInviteEmailCountAsync(factory, "newowner@club.example"));
    }

    [Fact]
    public async Task ResendOwnerInvite_WithNoEmailOnFile_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateTenantAsync(factory, client, "club-d");
        var createResponse = await client.PostAsJsonAsync(
            $"/api/platform/tenants/{organizationId:D}/owner-invites",
            new CreateOwnerInviteRequest(branchId, "newowner", "New Owner", null));
        var invite = await createResponse.Content.ReadFromJsonAsync<OwnerInviteDto>();

        var resend = await client.PostAsync($"/api/platform/owner-invites/{invite!.OwnerInviteId:D}/resend", null);

        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
    }
}
