using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformDebtEndpointTests
{
    [Fact]
    public async Task GET_debt_WithPermission_ReturnsClubInArrears()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var orgId = await BillingListEndpointTests.SeedOrgWithSubscriptionAsync(
                db, "debt-club", "Debt Club", SubscriptionStatusNames.PastDue);
            db.Invoices.Add(new InvoiceEntity
            {
                InvoiceId = Guid.NewGuid(), OrganizationId = orgId, Number = 1,
                Kind = InvoiceKindNames.Subscription,
                PeriodStartUtc = now.AddMonths(-1), PeriodEndUtc = now, IssuedAtUtc = now.AddDays(-17),
                DueAtUtc = now.AddDays(-10), AmountMinorUnits = 290000, GrossAmountMinorUnits = 290000,
                CurrencyCode = "TJS", Status = InvoiceStatusNames.Overdue, Description = "endpoint test",
                CreatedAtUtc = now.AddDays(-17), UpdatedAtUtc = now.AddDays(-17)
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/platform/debt");
        var rows = await response.Content.ReadFromJsonAsync<List<DebtRowDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(rows);
        Assert.Contains(rows!, row => row.OrganizationName == "Debt Club" && row.OutstandingMinorUnits == 290000);
    }

    [Fact]
    public async Task GET_debt_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/debt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_debt_WithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync("/api/platform/debt");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
