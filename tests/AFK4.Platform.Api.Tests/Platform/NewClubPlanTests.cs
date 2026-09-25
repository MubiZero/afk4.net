using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>Новый клуб на бесплатном тарифе — с лимитами тарифа, если платформа не задала своих.</summary>
public sealed class NewClubPlanTests
{
    [Fact]
    public async Task AFreeClub_WithoutExplicitLimits_GetsTheFreePlanLimits()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync("/api/platform/organizations", new CreateOrganizationRequest(
            "free-club", "Free Club", "main", "Main", "Dushanbe", OrganizationPlanCodeNames.Free, SubscriptionStatusNames.Active,
            Limits: null, OwnerUserName: "owner@free-club.test", OwnerDisplayName: "Owner", OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organizationId = (await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>())!.Organization.OrganizationId;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var organization = await db.Organizations.SingleAsync(candidate => candidate.OrganizationId == organizationId);
        var limits = OrganizationLimitsJson.Deserialize(organization.LimitsJson);
        Assert.Equal(1, limits.MaxBranches);
        Assert.Equal(ClubPlanLimits.FreeDevices, limits.MaxDevicesPerBranch);
        Assert.Equal(3, limits.MaxStaffUsersPerBranch);
        Assert.Equal(0, (await db.OrganizationSubscriptions.SingleAsync(candidate => candidate.OrganizationId == organizationId)).AmountMinorUnits);
    }
}
