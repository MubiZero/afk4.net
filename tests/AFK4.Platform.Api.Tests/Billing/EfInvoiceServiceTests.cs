using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfInvoiceServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfInvoiceService NewService(PlatformDbContext db, TimeProvider time, IInvoiceNotifier? notifier = null)
    {
        notifier ??= new RecordingInvoiceNotifier();
        return new(db, new EfInvoiceGenerationRunner(db, Options.Create(new BillingOptions()), notifier), notifier, time,
            Options.Create(new BillingOptions()));
    }

    private static async Task<Guid> SeedOrganizationWithSubscriptionAsync(PlatformDbContext db)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "o", Name = "O", Status = OrganizationStatusNames.Active,
            PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
            CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(), OrganizationId = orgId, PlanCode = "starter",
            Status = SubscriptionStatusNames.Active, CurrentPeriodStartUtc = Now, CurrentPeriodEndUtc = Now.AddMonths(1),
            NextInvoiceUtc = Now.AddMonths(1), AmountMinorUnits = 290000, CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly, CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return orgId;
    }

    [Fact]
    public async Task GenerateAsync_IssuesInvoiceForCurrentPeriod()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));

        var result = await service.GenerateAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(290000, result.Value!.AmountMinorUnits);
        Assert.Equal(InvoiceStatusNames.Issued, result.Value.Status);
    }

    [Fact]
    public async Task GenerateAsync_AlreadyIssuedForPeriod_ReturnsConflict()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        await service.GenerateAsync(orgId, CancellationToken.None);

        var subscription = await db.OrganizationSubscriptions.SingleAsync();
        subscription.CurrentPeriodStartUtc = Now;
        subscription.CurrentPeriodEndUtc = Now.AddMonths(1);
        await db.SaveChangesAsync();

        var result = await service.GenerateAsync(orgId, CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ListForOrganizationAsync_FiltersByStatus()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        await service.GenerateAsync(orgId, CancellationToken.None);

        var issued = await service.ListForOrganizationAsync(orgId, InvoiceStatusNames.Issued, CancellationToken.None);
        var paid = await service.ListForOrganizationAsync(orgId, InvoiceStatusNames.Paid, CancellationToken.None);

        Assert.Single(issued.Value!);
        Assert.Empty(paid.Value!);
    }

    [Fact]
    public async Task MarkPaidAsync_SetsPaidStatusAndTimestamp()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var time = new FixedTimeProvider(Now.AddDays(2));
        var service = NewService(db, time);
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);

        time.Now = Now.AddDays(3);
        var result = await service.MarkPaidAsync(generated.Value!.InvoiceId, new MarkInvoicePaidRequest("ref-1"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceStatusNames.Paid, result.Value!.Status);
        Assert.Equal(time.Now, result.Value.PaidAtUtc);
    }

    [Fact]
    public async Task MarkPaidAsync_NotifiesPaid()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)), notifier);
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);

        await service.MarkPaidAsync(generated.Value!.InvoiceId, new MarkInvoicePaidRequest("ref-1"), CancellationToken.None);

        var paid = Assert.Single(notifier.Paid);
        Assert.Equal(generated.Value.InvoiceId, paid.InvoiceId);
        Assert.Equal(InvoiceStatusNames.Paid, paid.Status);
    }

    [Fact]
    public async Task GenerateAsync_NotifiesIssued()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)), notifier);

        await service.GenerateAsync(orgId, CancellationToken.None);

        var issued = Assert.Single(notifier.Issued);
        Assert.Equal(InvoiceStatusNames.Issued, issued.Status);
        Assert.Empty(notifier.Paid);
    }

    [Fact]
    public async Task MarkPaidAsync_AlreadyPaid_ReturnsConflict()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);
        await service.MarkPaidAsync(generated.Value!.InvoiceId, new MarkInvoicePaidRequest(null), CancellationToken.None);

        var result = await service.MarkPaidAsync(generated.Value.InvoiceId, new MarkInvoicePaidRequest(null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task VoidAsync_RequiresReasonAndSetsVoidStatus()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var time = new FixedTimeProvider(Now.AddDays(2));
        var service = NewService(db, time);
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);

        var missingReason = await service.VoidAsync(generated.Value!.InvoiceId, new VoidInvoiceRequest("  "), CancellationToken.None);
        Assert.Equal(BillingOperationStatus.BadRequest, missingReason.Status);

        var result = await service.VoidAsync(generated.Value.InvoiceId, new VoidInvoiceRequest("duplicate"), CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceStatusNames.Void, result.Value!.Status);
        Assert.Equal("duplicate", result.Value.VoidReason);
    }

    [Fact]
    public async Task MarkPaidAsync_OverdueInvoice_CanBePaid()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);

        // Force the invoice into the overdue state.
        var stored = await db.Invoices.SingleAsync();
        stored.Status = InvoiceStatusNames.Overdue;
        await db.SaveChangesAsync();

        var result = await service.MarkPaidAsync(generated.Value!.InvoiceId, new MarkInvoicePaidRequest(null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceStatusNames.Paid, result.Value!.Status);
    }

    [Fact]
    public async Task VoidAsync_OverdueInvoice_CanBeVoided()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);

        var stored = await db.Invoices.SingleAsync();
        stored.Status = InvoiceStatusNames.Overdue;
        await db.SaveChangesAsync();

        var result = await service.VoidAsync(generated.Value!.InvoiceId, new VoidInvoiceRequest("overdue cleanup"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceStatusNames.Void, result.Value!.Status);
    }

    [Fact]
    public async Task ListForOrganizationAsync_InvalidStatus_ReturnsBadRequest()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));

        var result = await service.ListForOrganizationAsync(orgId, "garbage", CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task ListForOrganizationAsync_UnknownOrganization_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));

        var result = await service.ListForOrganizationAsync(Guid.NewGuid(), null, CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetBillingStatusAsync_NoDebt_ReportsNotInArrears()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.GetBillingStatusAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.InArrears);
        Assert.Equal(0, result.Value.OutstandingMinorUnits);
        Assert.Null(result.Value.OldestOverdueInvoiceNumber);
    }

    [Fact]
    public async Task GetBillingStatusAsync_OverdueInvoice_ReportsAmountNumberAndAge()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        await AddOverdueInvoiceAsync(db, orgId, number: 7);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.GetBillingStatusAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.InArrears);
        Assert.Equal(290000, result.Value.OutstandingMinorUnits);
        Assert.Equal("TJS", result.Value.CurrencyCode);
        Assert.Equal(7, result.Value.OldestOverdueInvoiceNumber);
        Assert.Equal(3, result.Value.DaysOverdue); // AddOverdueInvoiceAsync ставит срок на Now-3 дня
    }

    [Fact]
    public async Task GetBillingStatusAsync_UnknownOrganization_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.GetBillingStatusAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }

    private static async Task<InvoiceEntity> AddOverdueInvoiceAsync(PlatformDbContext db, Guid orgId, int number)
    {
        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = Now.AddMonths(-1),
            PeriodEndUtc = Now,
            IssuedAtUtc = Now.AddDays(-10),
            DueAtUtc = Now.AddDays(-3),
            AmountMinorUnits = 290000,
            GrossAmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            Status = InvoiceStatusNames.Overdue,
            Description = "d",
            CreatedAtUtc = Now.AddDays(-10),
            UpdatedAtUtc = Now.AddDays(-10)
        };
        db.Invoices.Add(invoice);

        var subscription = await db.OrganizationSubscriptions.SingleAsync();
        subscription.Status = SubscriptionStatusNames.PastDue;
        var organization = await db.Organizations.SingleAsync();
        organization.SubscriptionStatus = SubscriptionStatusNames.PastDue;
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task MarkPaidAsync_LastOverdueInvoicePaid_ReturnsSubscriptionToActive()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var invoice = await AddOverdueInvoiceAsync(db, orgId, number: 1);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.MarkPaidAsync(invoice.InvoiceId, new MarkInvoicePaidRequest(Reference: "cash"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.OrganizationSubscriptions.SingleAsync()).Status);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.Organizations.SingleAsync()).SubscriptionStatus);
    }

    [Fact]
    public async Task MarkPaidAsync_AnotherOverdueInvoiceRemains_KeepsPastDue()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var first = await AddOverdueInvoiceAsync(db, orgId, number: 1);
        await AddOverdueInvoiceAsync(db, orgId, number: 2);
        var service = NewService(db, new FixedTimeProvider(Now));

        await service.MarkPaidAsync(first.InvoiceId, new MarkInvoicePaidRequest(Reference: "cash"), CancellationToken.None);

        Assert.Equal(SubscriptionStatusNames.PastDue, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task VoidAsync_LastOverdueInvoiceVoided_ReturnsSubscriptionToActive()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var invoice = await AddOverdueInvoiceAsync(db, orgId, number: 1);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.VoidAsync(invoice.InvoiceId, new VoidInvoiceRequest("issued by mistake"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.OrganizationSubscriptions.SingleAsync()).Status);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.Organizations.SingleAsync()).SubscriptionStatus);
    }

    [Fact]
    public async Task VoidAsync_AnotherOverdueInvoiceRemains_KeepsPastDue()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var first = await AddOverdueInvoiceAsync(db, orgId, number: 1);
        await AddOverdueInvoiceAsync(db, orgId, number: 2);
        var service = NewService(db, new FixedTimeProvider(Now));

        await service.VoidAsync(first.InvoiceId, new VoidInvoiceRequest("issued by mistake"), CancellationToken.None);

        Assert.Equal(SubscriptionStatusNames.PastDue, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task CreateAsync_OneOff_IssuesPositiveInvoiceWithNextNumber()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff,
            AmountMinorUnits: 150000,
            Description: "Настройка оборудования",
            DueAtUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceKindNames.OneOff, result.Value!.Kind);
        Assert.Equal(InvoiceStatusNames.Issued, result.Value.Status);
        Assert.Equal(1, result.Value.Number);
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(150000, invoice.GrossAmountMinorUnits);
        Assert.Equal(0, invoice.DiscountMinorUnits);
        Assert.Equal(Now.AddDays(7), invoice.DueAtUtc);
    }

    [Fact]
    public async Task CreateAsync_Credit_AllowsNegativeAmountAndClearsArrears()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        await AddOverdueInvoiceAsync(db, orgId, number: 1);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.Credit,
            AmountMinorUnits: -290000,
            Description: "Компенсация простоя",
            DueAtUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(-290000, result.Value!.AmountMinorUnits);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(InvoiceKindNames.OneOff, -1)]
    [InlineData(InvoiceKindNames.OneOff, 0)]
    [InlineData(InvoiceKindNames.Credit, 1)]
    [InlineData(InvoiceKindNames.Credit, 0)]
    public async Task CreateAsync_AmountSignDoesNotMatchKind_IsRejected(string kind, long amount)
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: kind, AmountMinorUnits: amount, Description: "d", DueAtUtc: null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Theory]
    [InlineData(InvoiceKindNames.Subscription)]
    [InlineData(InvoiceKindNames.Proration)]
    public async Task CreateAsync_AutomaticKind_IsRejected(string kind)
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: kind, AmountMinorUnits: 150000, Description: "d", DueAtUtc: null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    // Regression for a review finding: MaxDescriptionLength (400) used to be looser than
    // PlatformDbContext's "invoices"."Description" column (HasMaxLength(240)). A 300-character
    // description passed this check but would fail at Postgres with 22001 ("value too long") — a
    // failure the InMemory provider used here cannot reproduce, so this test only proves the service
    // rejects it client-side at the same 240 limit as the column, not that Postgres would accept it.
    [Fact]
    public async Task CreateAsync_DescriptionLongerThanColumn_IsRejected()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff,
            AmountMinorUnits: 150000,
            Description: new string('d', 300),
            DueAtUtc: null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_DescriptionAtColumnLimit_IsAccepted()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff,
            AmountMinorUnits: 150000,
            Description: new string('d', 240),
            DueAtUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    // Regression: MarkPaidAsync did not look at Kind, so a credit note could be "marked paid" and
    // fall out of the balance calculation — turning a settled club back into arrears and resuming
    // the dunning ladder. Design spec §5: credit notes are issued and counted in the balance, never
    // paid.
    [Fact]
    public async Task MarkPaidAsync_CreditNote_ReturnsConflict()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));
        var credit = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.Credit,
            AmountMinorUnits: -150000,
            Description: "Компенсация простоя",
            DueAtUtc: null), CancellationToken.None);

        var result = await service.MarkPaidAsync(
            credit.Value!.InvoiceId, new MarkInvoicePaidRequest(null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
        var stored = await db.Invoices.SingleAsync(invoice => invoice.InvoiceId == credit.Value.InvoiceId);
        Assert.Equal(InvoiceStatusNames.Issued, stored.Status);
        Assert.Null(stored.PaidAtUtc);
    }

    [Fact]
    public async Task CreateAsync_BlankDescription_IsRejected()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff, AmountMinorUnits: 150000, Description: "   ", DueAtUtc: null),
            CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task CreateAsync_UnknownOrganization_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff, AmountMinorUnits: 150000, Description: "d", DueAtUtc: null),
            CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }

    // Regression for a code-review finding on task 7: EfInvoiceService.CreateAsync and
    // EfInvoiceGenerationRunner.GenerateForSubscriptionAsync both number the next invoice as
    // MAX(Number)+1 with no lock, while Invoices.Number carries a global unique index. A scheduler
    // tick generating a subscription invoice and an admin issuing a one-off/credit invoice at the
    // same moment can both read the same max and race to insert it — one must lose to the unique
    // index. Before the fix that loss surfaced as a raw DbUpdateException (500). The InMemory
    // provider every other test in this file uses does not enforce unique indexes at all, so it
    // cannot exercise this race — it needs a real PostgreSQL instance with a deterministic overlap
    // (a SaveChangesInterceptor gate, same pattern as
    // PlatformSupportAccessTicketTests.RedeemTicket_TwoConcurrentRedemptions_ExactlyOneSucceeds).
    [PlatformAdminPostgresFact]
    public async Task CreateAsync_TwoConcurrentOneOffInvoices_BothSucceedWithDistinctNumbers()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"invoice_numbering_race_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(rootBuilder.ConnectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var gate = new SaveOverlapGate();
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(scopedBuilder.ConnectionString)
                .AddInterceptors(gate)
                .Options;

            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            Guid orgOneId, orgTwoId;
            await using (var seedDb = new PlatformDbContext(options))
            {
                orgOneId = Guid.NewGuid();
                orgTwoId = Guid.NewGuid();
                seedDb.Organizations.AddRange(
                    new OrganizationEntity
                    {
                        OrganizationId = orgOneId, Slug = $"club-{orgOneId:N}", Name = "O1",
                        Status = OrganizationStatusNames.Active, PlanCode = "starter", LimitsJson = "{}",
                        CreatedAtUtc = Now, UpdatedAtUtc = Now
                    },
                    new OrganizationEntity
                    {
                        OrganizationId = orgTwoId, Slug = $"club-{orgTwoId:N}", Name = "O2",
                        Status = OrganizationStatusNames.Active, PlanCode = "starter", LimitsJson = "{}",
                        CreatedAtUtc = Now, UpdatedAtUtc = Now
                    });
                await seedDb.SaveChangesAsync();
            }

            // Two independent DbContexts (independent connections/transactions) each issuing a
            // one-off invoice at once — mirrors the nightly scheduler tick and a manual admin action
            // landing in the same instant.
            gate.Arm();
            await using var dbForFirst = new PlatformDbContext(options);
            await using var dbForSecond = new PlatformDbContext(options);
            var notifierOne = new RecordingInvoiceNotifier();
            var notifierTwo = new RecordingInvoiceNotifier();
            var billingOptions = Options.Create(new BillingOptions());
            var serviceForFirst = new EfInvoiceService(
                dbForFirst, new EfInvoiceGenerationRunner(dbForFirst, billingOptions, notifierOne),
                notifierOne, new FixedTimeProvider(Now), billingOptions);
            var serviceForSecond = new EfInvoiceService(
                dbForSecond, new EfInvoiceGenerationRunner(dbForSecond, billingOptions, notifierTwo),
                notifierTwo, new FixedTimeProvider(Now), billingOptions);

            var results = await Task.WhenAll(
                serviceForFirst.CreateAsync(orgOneId, new CreateInvoiceRequest(
                    InvoiceKindNames.OneOff, 150000, "Настройка A", null), CancellationToken.None),
                serviceForSecond.CreateAsync(orgTwoId, new CreateInvoiceRequest(
                    InvoiceKindNames.OneOff, 150000, "Настройка B", null), CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.All(results, result => Assert.True(result.Succeeded));
            var numbers = results.Select(result => result.Value!.Number).ToList();
            Assert.Equal(2, numbers.Distinct().Count());

            await using var verifyDb = new PlatformDbContext(options);
            var storedNumbers = await verifyDb.Invoices.AsNoTracking()
                .Select(invoice => invoice.Number).ToListAsync();
            Assert.Equal(2, storedNumbers.Distinct().Count());
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    // Blocks each SavingChangesAsync call until exactly two concurrent saves have arrived, then
    // releases both together — forces both transactions to have already read the same MAX(Number)
    // before either is allowed to flush its insert. Same pattern as
    // PlatformSupportAccessTicketTests.SaveOverlapGate / PlatformAdminDirectoryServiceTests.
    private sealed class SaveOverlapGate : SaveChangesInterceptor
    {
        private TaskCompletionSource? bothSavesReached;
        private int saveCount;
        private int armed;

        public void Arm()
        {
            bothSavesReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref saveCount, 0);
            Volatile.Write(ref armed, 1);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 0)
            {
                return result;
            }

            var reached = Interlocked.Increment(ref saveCount);
            if (reached == 2)
            {
                Volatile.Write(ref armed, 0);
                bothSavesReached!.TrySetResult();
            }

            await bothSavesReached!.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }

    // Regression for a review finding: when InvoiceNumbering.SaveAsync exhausts its retries,
    // GenerateAsync used to return Conflict without cleaning up — the invoice it had just Add()-ed
    // and the subscription's already-shifted CurrentPeriodStartUtc/CurrentPeriodEndUtc stayed dirty
    // on the tracked DbContext. Nothing forced that state to flush in these tests (each test disposes
    // its own DbContext), which is exactly why the bug shipped unnoticed — a later, unrelated
    // SaveChangesAsync on the same scoped DbContext would have persisted the never-issued invoice and
    // the shifted period anyway. This interceptor simulates the always-colliding-on-Number case
    // without needing a real Postgres unique index: SaveChangesInterceptor.SavingChangesAsync runs
    // before the provider-specific save regardless of provider, so it fires for InMemory too.
    [Fact]
    public async Task GenerateAsync_NumberingExhausted_CleansUpTrackedInvoiceAndSubscription()
    {
        var interceptor = new AlwaysCollidingOnNumberInterceptor();
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptor)
            .Options);
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var subscriptionBefore = await db.OrganizationSubscriptions.AsNoTracking().SingleAsync();
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));

        var result = await service.GenerateAsync(orgId, CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
        Assert.Empty(db.ChangeTracker.Entries<InvoiceEntity>());
        var subscriptionAfter = await db.OrganizationSubscriptions.AsNoTracking().SingleAsync();
        Assert.Equal(subscriptionBefore.CurrentPeriodStartUtc, subscriptionAfter.CurrentPeriodStartUtc);
        Assert.Equal(subscriptionBefore.CurrentPeriodEndUtc, subscriptionAfter.CurrentPeriodEndUtc);

        // Proves the tracker is actually clean, not just that this particular call returned early:
        // a follow-up save on the very same DbContext must not resurrect the abandoned invoice.
        interceptor.StopColliding();
        var retry = await service.GenerateAsync(orgId, CancellationToken.None);
        Assert.True(retry.Succeeded);
        Assert.Equal(1, retry.Value!.Number);
        Assert.Equal(1, await db.Invoices.CountAsync());
    }

    /// <summary>Throws a DbUpdateException wrapping a unique-violation PostgresException on the
    /// invoices-number index for every SaveChangesAsync call until <see cref="StopColliding"/> is
    /// called — deterministic stand-in for concurrent writers racing MAX(Number)+1 on real Postgres.</summary>
    private sealed class AlwaysCollidingOnNumberInterceptor : SaveChangesInterceptor
    {
        private bool colliding = true;

        public void StopColliding() => colliding = false;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (colliding && eventData.Context?.ChangeTracker.Entries<InvoiceEntity>()
                    .Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new DbUpdateException("simulated numbering collision", new PostgresException(
                    messageText: "duplicate key value violates unique constraint \"IX_invoices_Number\"",
                    severity: "ERROR",
                    invariantSeverity: "ERROR",
                    sqlState: PostgresErrorCodes.UniqueViolation,
                    detail: null,
                    hint: null,
                    position: 0,
                    internalPosition: 0,
                    internalQuery: null,
                    where: null,
                    schemaName: null,
                    tableName: "invoices",
                    columnName: null,
                    dataTypeName: null,
                    constraintName: "IX_invoices_Number",
                    file: null,
                    line: null,
                    routine: null));
            }

            return new ValueTask<InterceptionResult<int>>(result);
        }
    }
}
