using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfDunningRunnerTests
{
    private static readonly DateTimeOffset Due = DateTimeOffset.Parse("2026-05-10T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfDunningRunner NewRunner(PlatformDbContext db, RecordingInvoiceNotifier notifier) =>
        new(db, Options.Create(new BillingOptions()), notifier);

    private static async Task<InvoiceEntity> SeedAsync(
        PlatformDbContext db,
        string status = InvoiceStatusNames.Issued,
        DateTimeOffset? graceUntil = null)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "o", Name = "O", Status = OrganizationStatusNames.Active,
            PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
            CreatedAtUtc = Due, UpdatedAtUtc = Due
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = orgId,
            PlanCode = "starter",
            Status = SubscriptionStatusNames.Active,
            CurrentPeriodStartUtc = Due,
            CurrentPeriodEndUtc = Due.AddMonths(1),
            AmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            PaymentGraceUntilUtc = graceUntil,
            CreatedAtUtc = Due,
            UpdatedAtUtc = Due
        });
        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = 1,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = Due.AddMonths(-1),
            PeriodEndUtc = Due,
            IssuedAtUtc = Due.AddDays(-7),
            DueAtUtc = Due,
            AmountMinorUnits = 290000,
            GrossAmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            Status = status,
            Description = "d",
            CreatedAtUtc = Due.AddDays(-7),
            UpdatedAtUtc = Due.AddDays(-7)
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task RunAsync_ThreeDaysBeforeDue_SendsDueSoonOnce()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(-3), CancellationToken.None);
        await runner.RunAsync(Due.AddDays(-2), CancellationToken.None);

        Assert.Single(notifier.DueSoon);
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(Due.AddDays(-3), invoice.DueSoonNotifiedAtUtc);
        Assert.Equal(0, invoice.DunningStage);
    }

    [Fact]
    public async Task RunAsync_PastDueDate_FlipsToOverdueAndSendsStageOne()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddHours(1), CancellationToken.None);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatusNames.Overdue, invoice.Status);
        Assert.Equal(1, invoice.DunningStage);
        Assert.Equal(1, Assert.Single(notifier.Overdue).Stage);
    }

    [Fact]
    public async Task RunAsync_TenDaysOverdue_SendsOnlyHighestDueStage()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(10), CancellationToken.None);

        Assert.Equal(3, Assert.Single(notifier.Overdue).Stage); // offsets 0,3,7,14 → +10 days is rung 3
        Assert.Equal(3, (await db.Invoices.SingleAsync()).DunningStage);
    }

    [Fact]
    public async Task RunAsync_SameStageTwice_DoesNotResend()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(4), CancellationToken.None);
        await runner.RunAsync(Due.AddDays(5), CancellationToken.None);

        Assert.Single(notifier.Overdue);
    }

    [Fact]
    public async Task RunAsync_UnderGrace_SendsNothingAndKeepsStage()
    {
        await using var db = NewContext();
        await SeedAsync(db, graceUntil: Due.AddDays(30));
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(10), CancellationToken.None);

        Assert.Empty(notifier.Overdue);
        Assert.Empty(notifier.DueSoon);
        Assert.Equal(0, (await db.Invoices.SingleAsync()).DunningStage);
    }

    [Fact]
    public async Task RunAsync_AfterGraceExpires_ResumesAtAgeAppropriateStage()
    {
        await using var db = NewContext();
        await SeedAsync(db, graceUntil: Due.AddDays(5));
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(3), CancellationToken.None);   // silenced by grace
        await runner.RunAsync(Due.AddDays(16), CancellationToken.None);  // grace expired

        var sent = Assert.Single(notifier.Overdue);
        Assert.Equal(4, sent.Stage); // resumes at the rung its real age warrants, not at rung 1
    }

    [Fact]
    public async Task RunAsync_PaidInvoice_IsIgnored()
    {
        await using var db = NewContext();
        await SeedAsync(db, status: InvoiceStatusNames.Paid);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(10), CancellationToken.None);

        Assert.Empty(notifier.Overdue);
    }

    [Fact]
    public async Task RunAsync_FlipsIssuedInvoicesToOverdueAfterDueDate()
    {
        await using var db = NewContext();
        var invoice = await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(1), CancellationToken.None);

        var reloaded = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatusNames.Overdue, reloaded.Status);
        Assert.Equal(Due.AddDays(1), reloaded.UpdatedAtUtc);
    }

    [Fact]
    public async Task RunAsync_FlippingInvoiceToOverdue_NotifiesOverdueOncePerInvoice()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(1), CancellationToken.None); // flips to overdue, sends stage 1
        await runner.RunAsync(Due.AddDays(2), CancellationToken.None); // already stage 1, no resend

        var overdue = Assert.Single(notifier.Overdue);
        Assert.Equal(InvoiceStatusNames.Overdue, overdue.Invoice.Status);
    }
}
