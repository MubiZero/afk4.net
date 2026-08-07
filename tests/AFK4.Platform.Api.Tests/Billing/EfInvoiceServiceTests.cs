using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
}
