using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

/// <summary>«Приведи клуб»: приведённый клуб оплатил первый счёт — пригласившему месяц бесплатно.</summary>
public sealed class ClubReferralsTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-01T00:00:00Z");

    [Fact]
    public async Task TheFirstPaidInvoice_OfAReferredClub_GivesTheReferrerAFreeMonth_Once()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var referrer = Organization(db, "orion", referredBy: null);
        var referred = Organization(db, "victory", referredBy: referrer);
        var first = Invoice(db, referred, number: 1, amount: 4000);
        var second = Invoice(db, referred, number: 2, amount: 4000);
        await db.SaveChangesAsync();
        var options = Options.Create(new BillingOptions());
        var notifier = new RecordingInvoiceNotifier();
        var service = new EfInvoiceService(db, new EfInvoiceGenerationRunner(db, options, notifier), notifier, new FixedTimeProvider(Start.AddDays(3)), options);

        await service.MarkPaidAsync(first, new MarkInvoicePaidRequest("cash"), CancellationToken.None);
        await service.MarkPaidAsync(second, new MarkInvoicePaidRequest("cash"), CancellationToken.None);

        var referrerSubscription = await db.OrganizationSubscriptions.SingleAsync(candidate => candidate.OrganizationId == referrer);
        Assert.Equal(1, referrerSubscription.FreeMonths);

        // Бесплатный месяц обнуляет ближайший счёт пригласившего и тратится им.
        var runner = new EfInvoiceGenerationRunner(db, options, notifier);
        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);
        var invoice = await db.Invoices.SingleAsync(candidate => candidate.OrganizationId == referrer);
        Assert.Equal(0, invoice.AmountMinorUnits);
        Assert.Equal(0, referrerSubscription.FreeMonths);
    }

    [Fact]
    public async Task AReferralCode_IsGeneratedOnce_WithoutLookAlikeCharacters()
    {
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var organizationId = Organization(db, "orion", referredBy: null);
        await db.SaveChangesAsync();
        var organization = await db.Organizations.SingleAsync(candidate => candidate.OrganizationId == organizationId);

        var code = await ClubReferrals.EnsureCodeAsync(db, organization, CancellationToken.None);

        Assert.Matches("^AFK-[A-HJ-NP-Z2-9]{6}$", code);
        Assert.Equal(code, await ClubReferrals.EnsureCodeAsync(db, organization, CancellationToken.None));
        Assert.Equal(code, ClubReferrals.Normalize(" afk-" + code[4..].ToLowerInvariant() + " "));
    }

    private static Guid Organization(PlatformDbContext db, string slug, Guid? referredBy)
    {
        var id = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = id, Slug = slug, Name = slug, Status = OrganizationStatusNames.Active, PlanCode = "legacy_flat",
            SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}", ReferredByOrganizationId = referredBy,
            CreatedAtUtc = Start, UpdatedAtUtc = Start
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(), OrganizationId = id, PlanCode = "legacy_flat", Status = SubscriptionStatusNames.Active,
            CurrentPeriodStartUtc = Start, CurrentPeriodEndUtc = Start.AddMonths(1), NextInvoiceUtc = Start.AddMonths(1),
            AmountMinorUnits = 4000, CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly, CreatedAtUtc = Start, UpdatedAtUtc = Start
        });
        return id;
    }

    private static Guid Invoice(PlatformDbContext db, Guid organizationId, int number, long amount)
    {
        var id = Guid.NewGuid();
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = id, OrganizationId = organizationId, Number = number, Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = Start, PeriodEndUtc = Start.AddMonths(1), IssuedAtUtc = Start, DueAtUtc = Start.AddDays(7),
            AmountMinorUnits = amount, GrossAmountMinorUnits = amount, CurrencyCode = "TJS", Status = InvoiceStatusNames.Issued,
            Description = "test", CreatedAtUtc = Start, UpdatedAtUtc = Start
        });
        return id;
    }
}
