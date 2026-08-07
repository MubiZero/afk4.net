using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAnalyticsEndpointTests
{
    [Fact]
    public async Task GET_overview_WithPermission_ReturnsMonthlySeries()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var organizationId = await BillingListEndpointTests.SeedOrgWithSubscriptionAsync(
                db, "analytics-club", "Analytics Club", SubscriptionStatusNames.Active);
            db.Invoices.Add(new InvoiceEntity
            {
                InvoiceId = Guid.NewGuid(),
                OrganizationId = organizationId,
                Number = 1,
                Kind = InvoiceKindNames.Subscription,
                PeriodStartUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEndUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1),
                IssuedAtUtc = now,
                DueAtUtc = now.AddDays(7),
                AmountMinorUnits = 290000,
                GrossAmountMinorUnits = 290000,
                CurrencyCode = "TJS",
                Status = InvoiceStatusNames.Issued,
                Description = "analytics test",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/platform/analytics/overview?months=12");
        var overview = await response.Content.ReadFromJsonAsync<PlatformAnalyticsOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        Assert.Equal(12, overview!.Months.Count);
        Assert.Equal("TJS", overview.CurrencyCode);
        var currentMonth = overview.Months.Single(month => month.Year == now.Year && month.Month == now.Month);
        Assert.Equal(290000, currentMonth.RecurringMinorUnits);
    }

    [Fact]
    public async Task GET_overview_ClampsMonthsToSaneRange()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync("/api/platform/analytics/overview?months=999");
        var overview = await response.Content.ReadFromJsonAsync<PlatformAnalyticsOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        Assert.Equal(36, overview!.Months.Count);
    }

    [Fact]
    public async Task GET_overview_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/analytics/overview");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_overview_WithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync("/api/platform/analytics/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
