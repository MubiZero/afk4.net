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

    private static PlatformDbContext NewContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options);

    private static EfDunningRunner NewRunner(PlatformDbContext db, IInvoiceNotifier notifier) =>
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

    [Fact]
    public async Task RunAsync_ExactlyAtDueDate_FlipsToOverdueAndSendsStageOne()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due, CancellationToken.None);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatusNames.Overdue, invoice.Status);
        Assert.Equal(1, invoice.DunningStage);
        Assert.Equal(1, Assert.Single(notifier.Overdue).Stage);
    }

    [Fact]
    public async Task RunAsync_NotifierThrows_DoesNotAdvanceStageOrPersistFlip()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        await using (var db = NewContext(databaseName))
        {
            await SeedAsync(db);
            var notifier = new ThrowingInvoiceNotifier();
            var runner = NewRunner(db, notifier);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.RunAsync(Due.AddHours(1), CancellationToken.None));
        }

        // A fresh context against the same in-memory database sees only what was actually saved —
        // the entity mutated in the throwing run above was never flushed, so this must read the
        // original, unadvanced state.
        await using var verify = NewContext(databaseName);
        var invoice = await verify.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatusNames.Issued, invoice.Status);
        Assert.Equal(0, invoice.DunningStage);
    }

    private sealed class ThrowingInvoiceNotifier : IInvoiceNotifier
    {
        public Task NotifyIssuedAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not expected to be called.");

        public Task NotifyPaidAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not expected to be called.");

        public Task NotifyDueSoonAsync(InvoiceEntity invoice, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated notifier failure.");

        public Task NotifyOverdueAsync(InvoiceEntity invoice, int stage, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated notifier failure.");
    }

    [Fact]
    public async Task RunAsync_NotifierThrowsOnSecondInvoiceInBatch_KeepsFirstInvoiceProgressAndLeavesSecondUnadvanced()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        Guid firstSeededId;
        Guid secondSeededId;
        var notifier = new ThrowsOnSecondCallInvoiceNotifier();

        await using (var db = NewContext(databaseName))
        {
            var first = await SeedAsync(db);
            var second = await SeedAsync(db);
            firstSeededId = first.InvoiceId;
            secondSeededId = second.InvoiceId;

            var runner = NewRunner(db, notifier);

            // Two overdue invoices in one pass, processed one at a time: the notifier succeeds for
            // whichever invoice it sees first, then throws on the second. This is the exact shape
            // of the bug fixed twice already — a batched "mutate everyone, notify everyone, save
            // once" runner would have advanced both invoices' state before the exception, silently
            // losing the second invoice's notice.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.RunAsync(Due.AddHours(1), CancellationToken.None));
        }

        var sentInvoiceId = Assert.Single(notifier.SentOverdueInvoiceIds);
        var unsentInvoiceId = sentInvoiceId == firstSeededId ? secondSeededId : firstSeededId;

        // Fresh context against the same in-memory database — same reasoning as the single-invoice
        // throw test above: reading through the still-tracking context would show the in-memory,
        // never-saved mutation as if it had been persisted.
        await using var verify = NewContext(databaseName);
        var sentInvoice = await verify.Invoices.SingleAsync(invoice => invoice.InvoiceId == sentInvoiceId);
        var unsentInvoice = await verify.Invoices.SingleAsync(invoice => invoice.InvoiceId == unsentInvoiceId);

        Assert.Equal(InvoiceStatusNames.Overdue, sentInvoice.Status);
        Assert.Equal(1, sentInvoice.DunningStage);

        // Unadvanced means the next run recomputes the same stage for this invoice and notifies it
        // — the notice is delayed by one tick, not lost.
        Assert.Equal(InvoiceStatusNames.Issued, unsentInvoice.Status);
        Assert.Equal(0, unsentInvoice.DunningStage);
    }

    private sealed class ThrowsOnSecondCallInvoiceNotifier : IInvoiceNotifier
    {
        private int callCount;

        public List<Guid> SentOverdueInvoiceIds { get; } = [];

        public Task NotifyIssuedAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not expected to be called.");

        public Task NotifyPaidAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not expected to be called.");

        public Task NotifyDueSoonAsync(InvoiceEntity invoice, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not expected to be called.");

        public Task NotifyOverdueAsync(InvoiceEntity invoice, int stage, DateTimeOffset now, CancellationToken cancellationToken)
        {
            callCount++;
            if (callCount == 2)
            {
                throw new InvalidOperationException("Simulated failure on the second invoice in the batch.");
            }

            SentOverdueInvoiceIds.Add(invoice.InvoiceId);
            return Task.CompletedTask;
        }
    }
}
