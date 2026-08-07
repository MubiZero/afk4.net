using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfBillingMetricsServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static void AddSubscription(PlatformDbContext db, string status, long amount) =>
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            PlanCode = "starter",
            Status = status,
            CurrentPeriodStartUtc = Now,
            CurrentPeriodEndUtc = Now.AddMonths(1),
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

    [Fact]
    public async Task GetAsync_PastDueSubscription_IsCountedInMrr()
    {
        await using var db = NewContext();
        AddSubscription(db, SubscriptionStatusNames.Active, 290000);
        AddSubscription(db, SubscriptionStatusNames.PastDue, 290000);
        await db.SaveChangesAsync();

        var metrics = await new EfBillingMetricsService(db).GetAsync(CancellationToken.None);

        Assert.Equal(580000, metrics.MrrMinorUnits);
        Assert.Equal(2, metrics.ActiveSubscriptions);
    }

    [Fact]
    public async Task GetAsync_TrialAndCancelled_AreExcludedFromMrr()
    {
        await using var db = NewContext();
        AddSubscription(db, SubscriptionStatusNames.Trial, 290000);
        AddSubscription(db, SubscriptionStatusNames.Cancelled, 290000);
        await db.SaveChangesAsync();

        var metrics = await new EfBillingMetricsService(db).GetAsync(CancellationToken.None);

        Assert.Equal(0, metrics.MrrMinorUnits);
    }
}
