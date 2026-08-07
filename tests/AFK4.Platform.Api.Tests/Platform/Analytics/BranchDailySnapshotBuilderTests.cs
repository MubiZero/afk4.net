using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchDailySnapshotBuilderTests
{
    private static readonly Guid Organization = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Branch = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Клуб в UTC+5 без перехода на летнее время.
    private static BranchSnapshotBranch Dushanbe(DateTimeOffset createdAtUtc) =>
        new(Branch, Organization, "Asia/Dushanbe", createdAtUtc);

    private static BranchSnapshotInput Input(
        DateTimeOffset now,
        BranchSnapshotBranch branch,
        DateOnly? lastSnapshotDate = null,
        IReadOnlyList<BranchSnapshotEvent>? sessionStarts = null,
        IReadOnlyList<BranchSnapshotMoney>? payments = null,
        IReadOnlyList<BranchSnapshotMoney>? ledgerEntries = null,
        IReadOnlyList<BranchSnapshotEvent>? shiftOpens = null,
        DateTimeOffset? lastHeartbeatUtc = null) =>
        new(
            now,
            [branch],
            lastSnapshotDate is null
                ? new Dictionary<Guid, DateOnly>()
                : new Dictionary<Guid, DateOnly> { [branch.BranchId] = lastSnapshotDate.Value },
            sessionStarts ?? [],
            payments ?? [],
            ledgerEntries ?? [],
            shiftOpens ?? [],
            lastHeartbeatUtc is null
                ? new Dictionary<Guid, DateTimeOffset>()
                : new Dictionary<Guid, DateTimeOffset> { [branch.BranchId] = lastHeartbeatUtc.Value });

    [Fact]
    public void NewBranch_GetsOnlyYesterday_NotAFabricatedMonth()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero); // 08:00 в Душанбе
        var input = Input(now, Dushanbe(new DateTimeOffset(2026, 8, 6, 0, 0, 0, TimeSpan.Zero)));

        var facts = BranchDailySnapshotBuilder.Build(input);

        var day = Assert.Single(facts);
        Assert.Equal(new DateOnly(2026, 8, 7), day.Date);
    }

    [Fact]
    public void BranchWithHistory_BackfillsFromItsOwnLastDate()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)),
            lastSnapshotDate: new DateOnly(2026, 8, 4));

        var facts = BranchDailySnapshotBuilder.Build(input);

        Assert.Equal(
            new[] { new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 6), new DateOnly(2026, 8, 7) },
            facts.Select(fact => fact.Date).ToArray());
    }

    [Fact]
    public void Backfill_IsCappedAtThirtyDays()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastSnapshotDate: new DateOnly(2025, 1, 1));

        var facts = BranchDailySnapshotBuilder.Build(input);

        Assert.Equal(31, facts.Count); // окно [вчера-30; вчера] включительно
        Assert.Equal(new DateOnly(2026, 7, 8), facts[0].Date);
    }

    [Fact]
    public void Backfill_NeverStartsBeforeTheBranchExisted()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(now, Dushanbe(new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero)));

        // Филиала не было до 6 августа по местному времени (5 августа 20:00 UTC = 6 августа 01:00),
        // поэтому «нулевых суток» до его появления не выдумываем.
        var facts = BranchDailySnapshotBuilder.Build(
            input with { LastSnapshotDates = new Dictionary<Guid, DateOnly> { [Branch] = new DateOnly(2026, 7, 1) } });

        Assert.Equal(new DateOnly(2026, 8, 6), facts[0].Date);
    }

    [Fact]
    public void DayBoundary_FollowsBranchTimeZone_NotUtc()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        // 7 августа 21:00 UTC = 8 августа 02:00 в Душанбе — это уже СЕГОДНЯ по местному,
        // и во вчерашние сутки попасть не должно.
        var lateNight = new DateTimeOffset(2026, 8, 7, 21, 0, 0, TimeSpan.Zero);
        // 6 августа 20:00 UTC = 7 августа 01:00 по местному — это вчерашние сутки.
        var earlyMorning = new DateTimeOffset(2026, 8, 6, 20, 0, 0, TimeSpan.Zero);

        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            sessionStarts: [new BranchSnapshotEvent(Branch, lateNight), new BranchSnapshotEvent(Branch, earlyMorning)]);

        var day = Assert.Single(BranchDailySnapshotBuilder.Build(input));
        Assert.Equal(new DateOnly(2026, 8, 7), day.Date);
        Assert.Equal(1, day.SessionCount);
    }

    [Fact]
    public void Revenue_IsPosNetPlusGameplay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var atNoon = new DateTimeOffset(2026, 8, 7, 7, 0, 0, TimeSpan.Zero); // 12:00 в Душанбе

        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            payments: [new BranchSnapshotMoney(Branch, atNoon, "payment", 5_000L, "TJS")],
            ledgerEntries: [new BranchSnapshotMoney(Branch, atNoon, LedgerEntryTypeNames.GameplayCharge, -3_000L, "TJS")]);

        var day = Assert.Single(BranchDailySnapshotBuilder.Build(input));
        Assert.Equal(8_000L, day.RevenueMinorUnits);
        Assert.Equal("TJS", day.CurrencyCode);
    }

    [Fact]
    public void AgentAlive_IsTrue_WhenHeartbeatIsYoungerThanADay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastHeartbeatUtc: now.AddHours(-2));

        Assert.True(Assert.Single(BranchDailySnapshotBuilder.Build(input)).AgentAlive);
    }

    [Fact]
    public void AgentAlive_IsFalse_WhenHeartbeatIsOlderThanADay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastHeartbeatUtc: now.AddDays(-3));

        Assert.False(Assert.Single(BranchDailySnapshotBuilder.Build(input)).AgentAlive);
    }

    [Fact]
    public void AgentAlive_IsUnknown_ForBackfilledDays()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastSnapshotDate: new DateOnly(2026, 8, 4),
            lastHeartbeatUtc: now.AddHours(-1));

        var facts = BranchDailySnapshotBuilder.Build(input);

        // Живость меряется только «сейчас». Записать в позавчерашние сутки сегодняшний heartbeat —
        // выдумать факт; записать false — обвинить клуб в нашем простое.
        Assert.Null(facts[0].AgentAlive);
        Assert.Null(facts[1].AgentAlive);
        Assert.True(facts[^1].AgentAlive);
    }

    [Fact]
    public void UnknownTimeZoneId_FallsBackToUtc_WithoutThrowing()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var branch = new BranchSnapshotBranch(
            Branch, Organization, "Mars/Olympus", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var day = Assert.Single(BranchDailySnapshotBuilder.Build(Input(now, branch)));

        Assert.Equal(new DateOnly(2026, 8, 7), day.Date);
    }

    [Fact]
    public void ShiftOpens_AreCountedPerDay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var atNoon = new DateTimeOffset(2026, 8, 7, 7, 0, 0, TimeSpan.Zero);

        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            shiftOpens: [new BranchSnapshotEvent(Branch, atNoon), new BranchSnapshotEvent(Branch, atNoon.AddHours(8))]);

        Assert.Equal(2, Assert.Single(BranchDailySnapshotBuilder.Build(input)).ShiftOpenedCount);
    }

    [Fact]
    public void AgentAlive_IsUnknown_WhenBranchHasNoHeartbeatAtAll()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(now, Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        // Устройств не заводили — «мёртв» здесь было бы неправдой про клуб, который просто
        // ещё не разворачивали.
        Assert.Null(Assert.Single(BranchDailySnapshotBuilder.Build(input)).AgentAlive);
    }

    [Fact]
    public void Run_WritesNothing_WhenBranchIsAlreadySnapshottedThroughYesterday()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var branch = Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var throughYesterday = Input(now, branch, lastSnapshotDate: new DateOnly(2026, 8, 7));
        Assert.Empty(BranchDailySnapshotBuilder.Build(throughYesterday));

        var throughToday = Input(now, branch, lastSnapshotDate: new DateOnly(2026, 8, 8));
        Assert.Empty(BranchDailySnapshotBuilder.Build(throughToday));
    }

    [Fact]
    public void Build_ProcessesBranchesIndependently()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var otherBranch = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var atNoon = new DateTimeOffset(2026, 8, 7, 7, 0, 0, TimeSpan.Zero); // 12:00 в Душанбе

        var branchWithHistory = Dushanbe(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        var branchWithoutHistory = new BranchSnapshotBranch(
            otherBranch, Organization, "Asia/Dushanbe", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var input = new BranchSnapshotInput(
            now,
            [branchWithHistory, branchWithoutHistory],
            new Dictionary<Guid, DateOnly> { [Branch] = new DateOnly(2026, 8, 5) },
            [new BranchSnapshotEvent(Branch, atNoon), new BranchSnapshotEvent(otherBranch, atNoon)],
            [new BranchSnapshotMoney(Branch, atNoon, "payment", 1_000L, "TJS")],
            [],
            [],
            new Dictionary<Guid, DateTimeOffset>());

        var facts = BranchDailySnapshotBuilder.Build(input);

        var branchDays = facts.Where(fact => fact.BranchId == Branch).ToArray();
        var otherDays = facts.Where(fact => fact.BranchId == otherBranch).ToArray();

        Assert.Equal(
            new[] { new DateOnly(2026, 8, 6), new DateOnly(2026, 8, 7) },
            branchDays.Select(fact => fact.Date).ToArray());
        Assert.Equal(new DateOnly(2026, 8, 7), Assert.Single(otherDays).Date);

        var branchOnNoonDay = branchDays.Single(fact => fact.Date == new DateOnly(2026, 8, 7));
        Assert.Equal(1, branchOnNoonDay.SessionCount);
        Assert.Equal(1_000L, branchOnNoonDay.RevenueMinorUnits);

        // Событие и деньги первого филиала не должны утечь во второй.
        var otherOnNoonDay = Assert.Single(otherDays);
        Assert.Equal(1, otherOnNoonDay.SessionCount);
        Assert.Equal(0L, otherOnNoonDay.RevenueMinorUnits);
    }
}
