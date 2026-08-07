using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchSnapshotRunnerTests
{
    // 08:00 в Душанбе (UTC+5) — вчера по местному времени = 2026-08-07.
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Yesterday = new(2026, 8, 7);

    private static async Task<Guid> SeedBranchAsync(PlatformDbContext db, DateTimeOffset? createdAtUtc = null)
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Club",
            Status = OrganizationStatusNames.Active,
            CreatedAtUtc = Now.AddMonths(-3),
            UpdatedAtUtc = Now.AddMonths(-3)
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "branch-" + branchId.ToString("N")[..8],
            Name = "Branch",
            CreatedAtUtc = createdAtUtc ?? Now.AddMonths(-3)
        });
        await db.SaveChangesAsync();
        return branchId;
    }

    private static void SeedSession(PlatformDbContext db, Guid organizationId, Guid branchId, DateTimeOffset startedAtUtc)
    {
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "guest",
            TariffRuleVersionId = "standard-v1",
            State = "ended",
            RequestedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = startedAtUtc.AddHours(1)
        });
    }

    private static void SeedPayment(PlatformDbContext db, Guid organizationId, Guid branchId, DateTimeOffset createdAtUtc, long amount)
    {
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PosSaleId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PaymentKind = "payment",
            Provider = "cash",
            PaymentMethod = "cash",
            CurrencyCode = "TJS",
            AmountMinorUnits = amount,
            CreatedAtUtc = createdAtUtc
        });
    }

    private static async Task<Guid> GetOrganizationIdAsync(PlatformDbContext db, Guid branchId) =>
        (await db.Branches.SingleAsync(branch => branch.BranchId == branchId)).OrganizationId;

    [Fact]
    public async Task Run_WritesYesterdayForEveryBranch()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedBranchAsync(db);
        await SeedBranchAsync(db);
        var runner = scope.ServiceProvider.GetRequiredService<IBranchSnapshotRunner>();

        var written = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(2, written);
        var snapshots = await db.BranchDailySnapshots.ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.All(snapshots, snapshot => Assert.Equal(Yesterday, snapshot.SnapshotDate));
    }

    [Fact]
    public async Task Run_WritesNothing_WhenEveryBranchIsAlreadySnapshotted()
    {
        // Это НЕ тест защиты от гонки: InMemory-провайдер не знает уникальных индексов вовсе, так что
        // столкновение двух одновременных сохранений здесь физически не может произойти. Второй прогон
        // пишет 0 строк потому, что BranchDailySnapshotBuilder сам не переснимает уже снятые сутки
        // (см. BranchDailySnapshotBuilderTests) — гонку на настоящем уникальном индексе проверяет
        // Run_ConcurrentRunsForSameBranchAndDay_WriteExactlyOneRow (Postgres).
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedBranchAsync(db);
        var runner = scope.ServiceProvider.GetRequiredService<IBranchSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);
        var secondRun = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, secondRun);
        Assert.Equal(1, await db.BranchDailySnapshots.CountAsync());
    }

    [Fact]
    public async Task Run_BackfillsOnlyMissingDaysOfEachBranch()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Филиал A уже снят за позавчера — доснять нужно только вчера.
        var branchAId = await SeedBranchAsync(db);
        db.BranchDailySnapshots.Add(new BranchDailySnapshotEntity
        {
            BranchDailySnapshotId = Guid.NewGuid(),
            OrganizationId = await GetOrganizationIdAsync(db, branchAId),
            BranchId = branchAId,
            SnapshotDate = Yesterday.AddDays(-1),
            SessionCount = 0,
            RevenueMinorUnits = 0,
            CurrencyCode = "TJS",
            ShiftOpenedCount = 0,
            AgentAlive = true,
            CreatedAtUtc = Now.AddDays(-1)
        });

        // Филиал B без истории — тоже только вчера.
        var branchBId = await SeedBranchAsync(db);
        await db.SaveChangesAsync();

        var runner = scope.ServiceProvider.GetRequiredService<IBranchSnapshotRunner>();

        var written = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(2, written);

        var branchADates = await db.BranchDailySnapshots
            .Where(snapshot => snapshot.BranchId == branchAId)
            .Select(snapshot => snapshot.SnapshotDate)
            .OrderBy(date => date)
            .ToListAsync();
        Assert.Equal([Yesterday.AddDays(-1), Yesterday], branchADates);

        var branchBDates = await db.BranchDailySnapshots
            .Where(snapshot => snapshot.BranchId == branchBId)
            .Select(snapshot => snapshot.SnapshotDate)
            .ToListAsync();
        Assert.Equal([Yesterday], branchBDates);
    }

    [Fact]
    public async Task Run_CountsOnlyItsOwnBranchRows()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var branchAId = await SeedBranchAsync(db);
        var branchBId = await SeedBranchAsync(db);
        await db.SaveChangesAsync();

        var organizationAId = await GetOrganizationIdAsync(db, branchAId);
        var organizationBId = await GetOrganizationIdAsync(db, branchBId);

        // 12:00 в Душанбе вчера = 07:00 UTC вчера — попадает в вчерашние сутки обоих филиалов.
        var atNoonYesterday = Now.AddDays(-1).Date.AddHours(7);
        var atNoonYesterdayOffset = new DateTimeOffset(atNoonYesterday, TimeSpan.Zero);

        SeedSession(db, organizationAId, branchAId, atNoonYesterdayOffset);
        SeedPayment(db, organizationAId, branchAId, atNoonYesterdayOffset, 5_000);

        SeedSession(db, organizationBId, branchBId, atNoonYesterdayOffset);
        SeedPayment(db, organizationBId, branchBId, atNoonYesterdayOffset, 9_000);
        await db.SaveChangesAsync();

        var runner = scope.ServiceProvider.GetRequiredService<IBranchSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);

        var snapshotA = await db.BranchDailySnapshots.SingleAsync(snapshot => snapshot.BranchId == branchAId);
        var snapshotB = await db.BranchDailySnapshots.SingleAsync(snapshot => snapshot.BranchId == branchBId);

        Assert.Equal(1, snapshotA.SessionCount);
        Assert.Equal(5_000, snapshotA.RevenueMinorUnits);

        Assert.Equal(1, snapshotB.SessionCount);
        Assert.Equal(9_000, snapshotB.RevenueMinorUnits);
    }
}
