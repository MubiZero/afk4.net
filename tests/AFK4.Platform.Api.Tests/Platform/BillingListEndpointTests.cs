using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
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

        var service = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionService>();
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

        var service = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionService>();
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
        var service = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionService>();

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

        var service = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionService>();
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
            Status = TenantStatusNames.Active,
            PlanCode = planCode,
            SubscriptionStatus = status,
            LimitsJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
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
}
