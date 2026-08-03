using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSubscriptionEditingTests
{
    private static CreateOrganizationRequest BuildCreateOrganizationRequest(
        string orgSlug = "grace-club",
        string branchSlug = "grace-branch")
    {
        return new CreateOrganizationRequest(
            OrganizationSlug: orgSlug,
            OrganizationName: "Grace Club",
            BranchSlug: branchSlug,
            BranchName: "Grace Branch",
            BranchCity: "Dushanbe",
            PlanCode: OrganizationPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Trial,
            Limits: new OrganizationLimitsDto(3, 60, 80, 20),
            OwnerUserName: "owner@grace-club.test",
            OwnerDisplayName: "Grace Owner",
            OrganizationOwnerInviteLifetime: TimeSpan.FromDays(7));
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client, string slug = "grace-club")
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/organizations",
            BuildCreateOrganizationRequest(orgSlug: slug, branchSlug: $"{slug}-branch"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        Assert.NotNull(body);
        return body.Organization.OrganizationId;
    }

    [Fact]
    public async Task Patch_WithIndividualAmount_PersistsAndReturnsIt()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "amount-org");

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: 123456,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: null));
        var body = await response.Content.ReadFromJsonAsync<OrganizationSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(123456, body.AmountMinorUnits);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleAsync(s => s.OrganizationId == organizationId);
        Assert.Equal(123456, subscription.AmountMinorUnits);

        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == "billing.subscription.update" && record.Outcome == "Succeeded")
            .OrderByDescending(record => record.CreatedAtUtc)
            .FirstAsync();
        Assert.Contains("123456", audit.DetailsJson);
    }

    [Fact]
    public async Task Patch_ToTrial_WithPeriodEnd_Succeeds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "trial-org");

        var periodEnd = DateTimeOffset.UtcNow.AddDays(14);
        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: SubscriptionStatusNames.Trial,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: periodEnd,
                PaymentGraceUntilUtc: null));
        var body = await response.Content.ReadFromJsonAsync<OrganizationSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(SubscriptionStatusNames.Trial, body.Status);
        Assert.Equal(periodEnd, body.CurrentPeriodEndUtc);
    }

    [Fact]
    public async Task Patch_ToTrial_WithoutPeriodEnd_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "trial-no-end-org");

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: SubscriptionStatusNames.Trial,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_SetsPaymentGrace()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "grace-org");

        var graceUntil = DateTimeOffset.UtcNow.AddDays(5);
        var setResponse = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: graceUntil));
        var setBody = await setResponse.Content.ReadFromJsonAsync<OrganizationSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
        Assert.NotNull(setBody);
        Assert.Equal(graceUntil, setBody.PaymentGraceUntilUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleAsync(s => s.OrganizationId == organizationId);
        Assert.Equal(graceUntil, subscription.PaymentGraceUntilUtc);
    }

    [Fact]
    public async Task Patch_ClearPaymentGrace_RemovesIt()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "grace-clear-org");

        var graceUntil = DateTimeOffset.UtcNow.AddDays(5);
        var setResponse = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: graceUntil));
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);

        var clearResponse = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: null,
                ClearPaymentGrace: true));
        var clearBody = await clearResponse.Content.ReadFromJsonAsync<OrganizationSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.NotNull(clearBody);
        Assert.Null(clearBody.PaymentGraceUntilUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleAsync(s => s.OrganizationId == organizationId);
        Assert.Null(subscription.PaymentGraceUntilUtc);

        var audit = await dbContext.AuditRecords
            .Where(record => record.Action == "billing.subscription.update" && record.Outcome == "Succeeded")
            .OrderByDescending(record => record.CreatedAtUtc)
            .FirstAsync();
        Assert.Contains("\"PaymentGraceUntilUtc\":null", audit.DetailsJson);
    }

    [Fact]
    public async Task Patch_UnrelatedFields_DoNotClearExistingPaymentGrace()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "grace-untouched-org");

        var graceUntil = DateTimeOffset.UtcNow.AddDays(5);
        var setResponse = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: graceUntil));
        Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);

        var unrelatedResponse = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: true,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: null));
        var unrelatedBody = await unrelatedResponse.Content.ReadFromJsonAsync<OrganizationSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, unrelatedResponse.StatusCode);
        Assert.NotNull(unrelatedBody);
        Assert.True(unrelatedBody.CancelAtPeriodEnd);
        Assert.Equal(graceUntil, unrelatedBody.PaymentGraceUntilUtc);
    }

    [Fact]
    public async Task Patch_ClearPaymentGraceWithNewValue_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "grace-conflict-org");

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: DateTimeOffset.UtcNow.AddDays(5),
                ClearPaymentGrace: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithNegativeAmount_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "negative-amount-org");

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: -1,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithPaymentGraceInPast_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "grace-past-org");

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: null,
                PaymentGraceUntilUtc: DateTimeOffset.UtcNow.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithPeriodEndBeforeStart_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationAsync(client, "period-order-org");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleAsync(s => s.OrganizationId == organizationId);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new UpdateSubscriptionRequest(
                PlanCode: null,
                BillingInterval: null,
                Status: null,
                CancelAtPeriodEnd: null,
                AmountMinorUnits: null,
                CurrentPeriodEndUtc: subscription.CurrentPeriodStartUtc.AddDays(-1),
                PaymentGraceUntilUtc: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
