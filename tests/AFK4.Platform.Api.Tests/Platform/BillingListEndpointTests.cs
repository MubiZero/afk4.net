using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class BillingListEndpointTests
{
    [Fact]
    public async Task ListSubscriptions_returns_rows_with_org_identity()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = await SeedOrgWithSubscriptionAsync(db, "acme", "Acme", SubscriptionStatusNames.Active);

        var service = scope.ServiceProvider.GetRequiredService<IOrganizationSubscriptionService>();
        var result = await service.ListAsync(status: null, planCode: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        var row = Assert.Single(result.Value!, r => r.OrganizationId == org);
        Assert.Equal("Acme", row.OrganizationName);
        Assert.Equal("acme", row.OrganizationSlug);
    }

    [Fact]
    public async Task ListSubscriptions_filters_by_status()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedOrgWithSubscriptionAsync(db, "alpha-active", "Alpha", SubscriptionStatusNames.Active);
        await SeedOrgWithSubscriptionAsync(db, "beta-cancelled", "Beta", SubscriptionStatusNames.Cancelled);

        var service = scope.ServiceProvider.GetRequiredService<IOrganizationSubscriptionService>();
        var result = await service.ListAsync(status: SubscriptionStatusNames.Cancelled, planCode: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.All(result.Value!, r => Assert.Equal(SubscriptionStatusNames.Cancelled, r.Status));
        Assert.Contains(result.Value!, r => r.OrganizationSlug == "beta-cancelled");
        Assert.DoesNotContain(result.Value!, r => r.OrganizationSlug == "alpha-active");
    }

    [Fact]
    public async Task ListSubscriptions_rejects_unknown_status()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrganizationSubscriptionService>();

        var result = await service.ListAsync(status: "bogus", planCode: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task ListSubscriptions_filters_by_planCode()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedOrgWithSubscriptionAsync(db, "starter-org", "Starter", SubscriptionStatusNames.Active);
        await SeedOrgWithSubscriptionAsync(db, "growth-org", "Growth", SubscriptionStatusNames.Active, planCode: "growth");

        var service = scope.ServiceProvider.GetRequiredService<IOrganizationSubscriptionService>();
        var result = await service.ListAsync(status: null, planCode: "growth", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.All(result.Value!, r => Assert.Equal("growth", r.PlanCode));
        Assert.Contains(result.Value!, r => r.OrganizationSlug == "growth-org");
        Assert.DoesNotContain(result.Value!, r => r.OrganizationSlug == "starter-org");
    }

    internal static async Task<Guid> SeedOrgWithSubscriptionAsync(
        PlatformDbContext db, string slug, string name, string status, string planCode = "starter")
    {
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId,
            Slug = slug,
            Name = name,
            Status = OrganizationStatusNames.Active,
            PlanCode = planCode,
            SubscriptionStatus = status,
            LimitsJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = orgId,
            PlanCode = planCode,
            Status = status,
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
        return orgId;
    }

    [Fact]
    public async Task ListInvoices_returns_rows_with_org_identity_newest_first()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = await SeedOrgWithSubscriptionAsync(db, "inv-org", "Invoice Org", SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 1, status: InvoiceStatusNames.Issued);
        SeedInvoice(db, orgId, number: 2, status: InvoiceStatusNames.Paid);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var result = await service.ListAllAsync(status: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        var mine = result.Value!.Where(r => r.OrganizationId == orgId).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Equal(2, mine[0].Number); // newest (highest number) first
        Assert.Equal("Invoice Org", mine[0].OrganizationName);
    }

    [Fact]
    public async Task ListInvoices_filters_by_status_and_rejects_unknown()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = await SeedOrgWithSubscriptionAsync(db, "inv-filter", "Filter Org", SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 10, status: InvoiceStatusNames.Overdue);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var overdue = await service.ListAllAsync(status: InvoiceStatusNames.Overdue, CancellationToken.None);
        Assert.True(overdue.Succeeded);
        Assert.All(overdue.Value!, r => Assert.Equal(InvoiceStatusNames.Overdue, r.Status));

        var bad = await service.ListAllAsync(status: "nope", CancellationToken.None);
        Assert.False(bad.Succeeded);
        Assert.Equal(BillingOperationStatus.BadRequest, bad.Status);
    }

    [Fact]
    public async Task GET_subscriptions_requires_auth()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/platform/subscriptions");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_subscriptions_returns_rows_when_authorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var rows = await client.GetFromJsonAsync<List<SubscriptionListItemDto>>("/api/platform/subscriptions");
        Assert.NotNull(rows);
    }

    [Fact]
    public async Task GET_invoices_returns_rows_when_authorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var rows = await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/platform/invoices");
        Assert.NotNull(rows);
    }

    [Fact]
    public async Task GET_subscriptions_rejects_bad_status_filter()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var response = await client.GetAsync("/api/platform/subscriptions?status=bogus");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void SeedInvoice(PlatformDbContext db, Guid orgId, int number, string status)
    {
        var now = DateTimeOffset.UtcNow;
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = now.AddMonths(-1),
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = now.AddDays(7),
            AmountMinorUnits = 290000,
            CurrencyCode = "RUB",
            Status = status,
            Description = "Test invoice",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }
}
