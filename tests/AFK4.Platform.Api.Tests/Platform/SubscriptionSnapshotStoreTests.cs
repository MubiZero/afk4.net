using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SubscriptionSnapshotStoreTests
{
    [Fact]
    public async Task Snapshot_RoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.SubscriptionDailySnapshots.Add(new SubscriptionDailySnapshotEntity
        {
            SubscriptionDailySnapshotId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            SnapshotDate = new DateOnly(2026, 8, 7),
            Status = SubscriptionStatusNames.Active,
            PlanCode = "pro",
            MonthlyAmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var stored = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 7), stored.SnapshotDate);
        Assert.Equal(290000, stored.MonthlyAmountMinorUnits);
    }
}
