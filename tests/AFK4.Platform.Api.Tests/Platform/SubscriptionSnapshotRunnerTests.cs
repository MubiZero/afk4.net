using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SubscriptionSnapshotRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 3, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedSubscriptionAsync(
        PlatformDbContext db, string status, long amount, string interval, int? discountPercent = null)
    {
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Club",
            Status = OrganizationStatusNames.Active,
            CreatedAtUtc = Now.AddMonths(-3),
            UpdatedAtUtc = Now.AddMonths(-3)
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            PlanCode = "pro",
            Status = status,
            CurrentPeriodStartUtc = Now.AddDays(-10),
            CurrentPeriodEndUtc = Now.AddDays(20),
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            BillingInterval = interval,
            DiscountPercent = discountPercent,
            DiscountUntilUtc = discountPercent is null ? null : Now.AddMonths(6),
            CreatedAtUtc = Now.AddMonths(-3),
            UpdatedAtUtc = Now.AddMonths(-3)
        });
        await db.SaveChangesAsync();
        return organizationId;
    }

    [Fact]
    public async Task Run_WritesOneSnapshotPerOrganizationForYesterday()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 290000, BillingIntervalNames.Monthly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        var written = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, written);
        var snapshot = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 6), snapshot.SnapshotDate);
        Assert.Equal(SubscriptionStatusNames.Active, snapshot.Status);
        Assert.Equal(290000, snapshot.MonthlyAmountMinorUnits);
    }

    [Fact]
    public async Task Run_Twice_DoesNotDuplicateSnapshot()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 290000, BillingIntervalNames.Monthly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);
        var secondRun = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, secondRun);
        Assert.Equal(1, await db.SubscriptionDailySnapshots.CountAsync());
    }

    [Fact]
    public async Task Run_NormalizesYearlyPlanToMonthlyAmount()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 3480000, BillingIntervalNames.Yearly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);

        var snapshot = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(290000, snapshot.MonthlyAmountMinorUnits);
    }

    [Fact]
    public async Task Run_AppliesActiveDiscount()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 300000, BillingIntervalNames.Monthly, discountPercent: 10);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);

        var snapshot = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(270000, snapshot.MonthlyAmountMinorUnits);
    }

    [Fact]
    public async Task Run_BackfillsMissedDay()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 290000, BillingIntervalNames.Monthly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        // Процесс стоял двое суток: снимок за пропущенный день всё равно должен появиться,
        // иначе в графике оттока навсегда останется дыра.
        await runner.RunAsync(Now.AddDays(-1), CancellationToken.None);
        var written = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, written);
        var dates = await db.SubscriptionDailySnapshots.Select(s => s.SnapshotDate).OrderBy(d => d).ToListAsync();
        Assert.Equal([new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 6)], dates);
    }
}
