using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Identity.AccountActivation;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OrganizationOwnerInviteEmailEndpointTests
{
    private static CreateOrganizationRequest BuildCreateOrganizationRequest(string slug) => new(
        OrganizationSlug: slug,
        OrganizationName: "Demo Club",
        BranchSlug: $"{slug}-branch",
        BranchName: "Demo Branch",
        BranchCity: "Dushanbe",
        PlanCode: OrganizationPlanCodeNames.Starter,
        SubscriptionStatus: SubscriptionStatusNames.Trial,
        Limits: new OrganizationLimitsDto(MaxBranches: 3, MaxDevicesPerBranch: 60, MaxConcurrentSessions: 80, MaxStaffUsersPerBranch: 20),
        OwnerUserName: "owner@demo.test",
        OwnerDisplayName: "Demo Owner",
        OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));

    private static async Task<(Guid OrganizationId, Guid BranchId)> CreateOrganizationAsync(PlatformApiFactory factory, HttpClient client, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/platform/organizations", BuildCreateOrganizationRequest(slug));
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        return (body!.Organization.OrganizationId, body.Organization.Branches[0].BranchId);
    }

    private static async Task<int> OrganizationOwnerInviteEmailCountAsync(PlatformApiFactory factory, string toAddress)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.NotificationOutbox
            .CountAsync(row => row.TemplateKey == NotificationTemplateKeys.OrganizationOwnerInvite && row.RecipientAddress == toAddress);
    }

    [Fact]
    public async Task CreateOrganizationOwnerInvite_WithEmail_EnqueuesOrganizationOwnerInviteEmailCarryingTheCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateOrganizationAsync(factory, client, "club-a");

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(branchId, "newowner", "New Owner", null, "newowner@club.example"));
        var invite = await response.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await OrganizationOwnerInviteEmailCountAsync(factory, "newowner@club.example"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.NotificationOutbox.SingleAsync(r => r.TemplateKey == NotificationTemplateKeys.OrganizationOwnerInvite);
        Assert.Contains(invite!.Code, row.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateOrganizationOwnerInvite_WithoutEmail_DoesNotEnqueueEmail()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateOrganizationAsync(factory, client, "club-b");

        var response = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(branchId, "newowner", "New Owner", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(0, await db.NotificationOutbox.CountAsync(r => r.TemplateKey == NotificationTemplateKeys.OrganizationOwnerInvite));
    }

    [Fact]
    public async Task ResendOrganizationOwnerInvite_EnqueuesAnotherEmail()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateOrganizationAsync(factory, client, "club-c");
        var createResponse = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(branchId, "newowner", "New Owner", null, "newowner@club.example"));
        var invite = await createResponse.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();

        var resend = await client.PostAsync($"/api/platform/organization-owner-invitations/{invite!.OrganizationOwnerInviteId:D}/resend", null);

        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Equal(2, await OrganizationOwnerInviteEmailCountAsync(factory, "newowner@club.example"));
    }

    [Fact]
    public async Task ResendOrganizationOwnerInvite_WithNoEmailOnFile_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var (organizationId, branchId) = await CreateOrganizationAsync(factory, client, "club-d");
        var createResponse = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/organization-owner-invitations",
            new CreateOrganizationOwnerInviteRequest(branchId, "newowner", "New Owner", null));
        var invite = await createResponse.Content.ReadFromJsonAsync<OrganizationOwnerInviteDto>();

        var resend = await client.PostAsync($"/api/platform/organization-owner-invitations/{invite!.OrganizationOwnerInviteId:D}/resend", null);

        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
    }
}
