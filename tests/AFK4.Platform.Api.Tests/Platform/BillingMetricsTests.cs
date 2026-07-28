using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class BillingMetricsTests
{
    [Fact]
    public async Task Metrics_sum_mrr_outstanding_and_overdue()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var activeOrg = await BillingListEndpointTests.SeedOrgWithSubscriptionAsync(
            db, "mrr-active", "MRR Active", SubscriptionStatusNames.Active);
        await BillingListEndpointTests.SeedOrgWithSubscriptionAsync(
            db, "mrr-cancelled", "MRR Cancelled", SubscriptionStatusNames.Cancelled);

        var now = DateTimeOffset.UtcNow;
        db.Invoices.Add(MakeInvoice(activeOrg, 1001, InvoiceStatusNames.Issued, 290000, now));
        db.Invoices.Add(MakeInvoice(activeOrg, 1002, InvoiceStatusNames.Overdue, 150000, now));
        db.Invoices.Add(MakeInvoice(activeOrg, 1003, InvoiceStatusNames.Paid, 999999, now)); // excluded
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IBillingMetricsService>();
        var metrics = await service.GetAsync(CancellationToken.None);

        Assert.Equal(290000L, metrics.MrrMinorUnits);
        Assert.Equal("RUB", metrics.CurrencyCode);
        Assert.Equal(1, metrics.ActiveSubscriptions);
        Assert.Equal(290000L + 150000L, metrics.OutstandingMinorUnits);
        Assert.Equal(2, metrics.OutstandingCount);
        Assert.Equal(150000L, metrics.OverdueMinorUnits);
        Assert.Equal(1, metrics.OverdueCount);
    }

    private static InvoiceEntity MakeInvoice(Guid orgId, int number, string status, long amount, DateTimeOffset now) =>
        new()
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = now.AddMonths(-1),
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = now.AddDays(7),
            AmountMinorUnits = amount,
            CurrencyCode = "RUB",
            Status = status,
            Description = "metrics test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    [Fact]
    public async Task GET_metrics_returns_payload_when_authorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var metrics = await client.GetFromJsonAsync<PlatformBillingMetricsDto>("/api/platform/metrics");
        Assert.NotNull(metrics);
    }

    [Fact]
    public async Task GET_metrics_requires_auth()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/platform/metrics");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
