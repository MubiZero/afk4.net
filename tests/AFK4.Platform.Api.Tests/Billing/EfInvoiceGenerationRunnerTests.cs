using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfInvoiceGenerationRunnerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfInvoiceGenerationRunner NewRunner(PlatformDbContext db, IInvoiceNotifier? notifier = null) =>
        new(db, Options.Create(new BillingOptions()), notifier ?? new RecordingInvoiceNotifier());

    private static async Task<OrganizationSubscriptionEntity> SeedActiveDueSubscriptionAsync(PlatformDbContext db)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "o", Name = "O", Status = OrganizationStatusNames.Active,
            PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
            CreatedAtUtc = Start, UpdatedAtUtc = Start
        });
        var subscription = new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = orgId,
            PlanCode = "starter",
            Status = SubscriptionStatusNames.Active,
            CurrentPeriodStartUtc = Start,
            CurrentPeriodEndUtc = Start.AddMonths(1),
            NextInvoiceUtc = Start.AddMonths(1),
            AmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            CreatedAtUtc = Start,
            UpdatedAtUtc = Start
        };
        db.OrganizationSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription;
    }

    [Fact]
    public async Task RunAsync_DueSubscription_IssuesInvoiceAndAdvancesPeriod()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);

        var issued = await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);

        Assert.Equal(1, issued);
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceKindNames.Subscription, invoice.Kind);
        Assert.Equal(290000, invoice.AmountMinorUnits);
        Assert.Equal(1, invoice.Number);
        Assert.Equal(Start.AddMonths(1), invoice.IssuedAtUtc);
        Assert.Equal(Start.AddMonths(1).AddDays(7), invoice.DueAtUtc); // default InvoiceDueAfter = 7 days
        var reloaded = await db.OrganizationSubscriptions.SingleAsync();
        Assert.Equal(Start.AddMonths(1), reloaded.CurrentPeriodStartUtc);
        Assert.Equal(Start.AddMonths(2), reloaded.CurrentPeriodEndUtc);
    }

    [Fact]
    public async Task RunAsync_NotYetDue_IssuesNothing()
    {
        await using var db = NewContext();
        await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);

        var issued = await runner.RunAsync(Start.AddDays(5), CancellationToken.None);

        Assert.Equal(0, issued);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RunAsync_RunTwiceSamePeriod_DoesNotDoubleIssue()
    {
        await using var db = NewContext();
        await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);

        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);
        var second = await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Equal(1, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RunAsync_DoesNotFlipInvoicesToOverdue()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);
        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None); // issues invoice due +7 days

        await runner.RunAsync(Start.AddMonths(1).AddDays(8), CancellationToken.None);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatusNames.Issued, invoice.Status);
    }

    [Fact]
    public async Task RunAsync_IssuingInvoice_NotifiesIssued()
    {
        await using var db = NewContext();
        await SeedActiveDueSubscriptionAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);

        var notified = Assert.Single(notifier.Issued);
        Assert.Equal(1, notified.Number);
        Assert.Empty(notifier.Overdue);
    }

    [Fact]
    public async Task RunAsync_CancelledSubscription_IsSkipped()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        subscription.Status = SubscriptionStatusNames.Cancelled;
        await db.SaveChangesAsync();
        var runner = NewRunner(db);

        var issued = await runner.RunAsync(Start.AddMonths(2), CancellationToken.None);

        Assert.Equal(0, issued);
    }
}
