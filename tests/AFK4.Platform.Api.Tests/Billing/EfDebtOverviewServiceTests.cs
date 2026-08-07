using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfDebtOverviewServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-15T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static Guid SeedClub(
        PlatformDbContext db,
        string name,
        string organizationStatus = OrganizationStatusNames.Active,
        string subscriptionStatus = SubscriptionStatusNames.PastDue,
        DateTimeOffset? graceUntil = null)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = name.ToLowerInvariant(), Name = name,
            Status = organizationStatus, PlanCode = "starter", SubscriptionStatus = subscriptionStatus,
            LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(), OrganizationId = orgId, PlanCode = "starter",
            Status = subscriptionStatus, CurrentPeriodStartUtc = Now.AddMonths(-1), CurrentPeriodEndUtc = Now,
            AmountMinorUnits = 290000, CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly,
            PaymentGraceUntilUtc = graceUntil, CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        return orgId;
    }

    private static void SeedInvoice(
        PlatformDbContext db,
        Guid orgId,
        int number,
        long amountMinorUnits,
        string status,
        DateTimeOffset dueAtUtc,
        int dunningStage = 0,
        string kind = InvoiceKindNames.Subscription) =>
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(), OrganizationId = orgId, Number = number, Kind = kind,
            PeriodStartUtc = dueAtUtc.AddMonths(-1), PeriodEndUtc = dueAtUtc, IssuedAtUtc = dueAtUtc.AddDays(-7),
            DueAtUtc = dueAtUtc, AmountMinorUnits = amountMinorUnits, GrossAmountMinorUnits = amountMinorUnits,
            CurrencyCode = "TJS", Status = status, Description = "d", DunningStage = dunningStage,
            CreatedAtUtc = dueAtUtc.AddDays(-7), UpdatedAtUtc = dueAtUtc.AddDays(-7)
        });

    [Fact]
    public async Task GetAsync_ClubInArrears_ReportsBalanceAgeAndStage()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена");
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-10), dunningStage: 3);
        await db.SaveChangesAsync();

        var rows = await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("Арена", row.OrganizationName);
        Assert.Equal(290000, row.OutstandingMinorUnits);
        Assert.Equal(1, row.OldestOverdueInvoiceNumber);
        Assert.Equal(10, row.DaysOverdue);
        Assert.Equal(3, row.DunningStage);
        Assert.Null(row.GraceUntilUtc);
        Assert.False(row.SettledButSuspended);
    }

    [Fact]
    public async Task GetAsync_CreditNoteCoversDebt_ClubIsNotListed()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена");
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-10));
        SeedInvoice(db, orgId, number: 2, amountMinorUnits: -290000, status: InvoiceStatusNames.Issued,
            dueAtUtc: Now, kind: InvoiceKindNames.Credit);
        await db.SaveChangesAsync();

        var rows = await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetAsync_GraceInForce_ClubIsListedAndMarked()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена", subscriptionStatus: SubscriptionStatusNames.Active,
            graceUntil: Now.AddDays(20));
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-10));
        await db.SaveChangesAsync();

        var row = Assert.Single(await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None));

        Assert.Equal(Now.AddDays(20), row.GraceUntilUtc);
    }

    [Fact]
    public async Task GetAsync_SuspendedClubWithoutDebt_IsListedAsSettledButSuspended()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена", organizationStatus: OrganizationStatusNames.Suspended,
            subscriptionStatus: SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Paid,
            dueAtUtc: Now.AddDays(-10));
        await db.SaveChangesAsync();

        var row = Assert.Single(await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None));

        Assert.True(row.SettledButSuspended);
        Assert.Equal(0, row.OutstandingMinorUnits);
    }

    [Fact]
    public async Task GetAsync_HealthyClub_IsNotListed()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена", subscriptionStatus: SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Paid,
            dueAtUtc: Now.AddDays(-10));
        await db.SaveChangesAsync();

        Assert.Empty(await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_IssuedInvoiceNotYetDue_ClubIsNotListed()
    {
        // Due date is in the future and the dunning sweep hasn't flipped the status to Overdue yet —
        // the invoice's own status stays authoritative, so the debt queue must show the same picture
        // as the dunning ladder. A mismatch here would be worse than an hour's delay.
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена");
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Issued,
            dueAtUtc: Now.AddDays(10));
        await db.SaveChangesAsync();

        Assert.Empty(await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_SeveralClubs_OldestDebtComesFirst()
    {
        await using var db = NewContext();
        var fresh = SeedClub(db, "Свежий");
        var old = SeedClub(db, "Старый");
        SeedInvoice(db, fresh, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-2));
        SeedInvoice(db, old, number: 2, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-30));
        await db.SaveChangesAsync();

        var rows = await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None);

        Assert.Equal(["Старый", "Свежий"], rows.Select(row => row.OrganizationName).ToArray());
    }
}
