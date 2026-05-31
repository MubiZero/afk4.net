using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class ClubBillingEndpointTests
{
    [Fact]
    public async Task GET_subscription_requires_auth()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/subscription");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_subscription_as_owner_returns_own_subscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedSubscriptionAsync(factory);

        var subscription = await client.GetFromJsonAsync<TenantSubscriptionDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/subscription");

        Assert.NotNull(subscription);
        Assert.Equal(TestIds.OrganizationId, subscription!.OrganizationId);
    }

    [Fact]
    public async Task GET_subscription_rejects_other_org_with_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedSubscriptionAsync(factory);

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/subscription");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_subscription_without_permission_returns_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/subscription");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_invoices_as_owner_returns_rows()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedSubscriptionAsync(factory);
        await SeedInvoiceAsync(factory);

        var invoices = await client.GetFromJsonAsync<List<InvoiceDto>>(
            $"/api/organizations/{TestIds.OrganizationId:D}/invoices");

        Assert.NotNull(invoices);
        Assert.Contains(invoices!, i => i.OrganizationId == TestIds.OrganizationId);
    }

    [Fact]
    public async Task GET_invoices_requires_auth()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_invoices_rejects_other_org_with_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedSubscriptionAsync(factory);

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/invoices");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedSubscriptionAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            PlanCode = "starter",
            Status = SubscriptionStatusNames.Active,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = now.AddMonths(1),
            NextInvoiceUtc = now.AddMonths(1),
            AmountMinorUnits = 290000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedInvoiceAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            Number = 5001,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = now.AddMonths(-1),
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = now.AddDays(7),
            AmountMinorUnits = 290000,
            CurrencyCode = "RUB",
            Status = InvoiceStatusNames.Issued,
            Description = "club billing test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
    }
}
