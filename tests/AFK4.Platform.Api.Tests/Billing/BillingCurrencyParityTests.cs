using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

// Regression: the club-side billing banner (EfInvoiceService.GetBillingStatusAsync) and the platform
// debt queue (EfDebtOverviewService.GetAsync) both fall back to a default currency when neither the
// oldest overdue invoice nor the subscription carries one. That fallback used to be a hardcoded "TJS"
// literal in EfDebtOverviewService while EfInvoiceService already read it from
// IOptions<BillingOptions>.DefaultCurrencyCode — the two agreed only by coincidence, and a
// Billing:DefaultCurrencyCode config change would have made the platform panel and the club's own
// banner disagree about the currency of the very same debt, with no test catching it.
public sealed class BillingCurrencyParityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    [Fact]
    public async Task GetBillingStatusAsync_And_DebtOverview_AgreeOnConfiguredFallbackCurrency()
    {
        await using var db = NewContext();
        var orgId = Guid.NewGuid();
        // Suspended with no subscription and no invoices: no invoice/subscription currency to read
        // from, so both services must fall back to the configured default — and suspended-without-debt
        // is exactly the case EfDebtOverviewService still lists (SettledButSuspended), so this club
        // reaches the fallback path in both readers.
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "no-sub-club", Name = "No Sub Club",
            Status = OrganizationStatusNames.Suspended, PlanCode = "starter",
            SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
            CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();

        var billingOptions = Options.Create(new BillingOptions { DefaultCurrencyCode = "USD" });
        var notifier = new RecordingInvoiceNotifier();
        var invoiceService = new EfInvoiceService(
            db, new EfInvoiceGenerationRunner(db, billingOptions, notifier), notifier,
            new FixedTimeProvider(Now), billingOptions);
        var debtOverviewService = new EfDebtOverviewService(db, billingOptions);

        var status = await invoiceService.GetBillingStatusAsync(orgId, CancellationToken.None);
        var rows = await debtOverviewService.GetAsync(Now, CancellationToken.None);

        Assert.True(status.Succeeded);
        var row = Assert.Single(rows);
        Assert.True(row.SettledButSuspended);
        Assert.Equal("USD", status.Value!.CurrencyCode);
        Assert.Equal("USD", row.CurrencyCode);
        Assert.Equal(status.Value.CurrencyCode, row.CurrencyCode);
    }
}
