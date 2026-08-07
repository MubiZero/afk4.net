using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformHealthRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    private static HealthSnapshot Snapshot(params JobState[] jobs) => new(jobs, 0, 0, 0, 0, Now);

    private static HealthSnapshot Snapshot(DateTimeOffset processStartedAtUtc, params JobState[] jobs) =>
        new(jobs, 0, 0, 0, 0, processStartedAtUtc);

    [Fact]
    public void FreshSuccess_ProducesNoProblem()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), Now.AddMinutes(-10), 0)), Now);

        Assert.Empty(problems);
    }

    [Fact]
    public void StaleJob_ProducesOverdueProblem()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), Now.AddHours(-5), 0)), Now);

        var problem = Assert.Single(problems);
        Assert.Equal(PlatformIncidentKindNames.JobOverdue, problem.Kind);
        Assert.Equal("job_overdue:daily_summary", problem.DedupKey);
        Assert.Equal(PlatformIncidentSeverityNames.Warning, problem.Severity);
    }

    [Fact]
    public void StaleMoneyJob_IsCritical()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.InvoiceGeneration, TimeSpan.FromHours(1), Now.AddHours(-5), 0)), Now);

        Assert.Equal(PlatformIncidentSeverityNames.Critical, Assert.Single(problems).Severity);
    }

    [Fact]
    public void FastJob_UsesMinimumWindowInsteadOfTripleInterval()
    {
        // Тройной интервал у outbox — 30 секунд; без нижней границы окна любая пауза
        // в полминуты порождала бы инцидент.
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.BillingOutbox, TimeSpan.FromSeconds(10), Now.AddMinutes(-5), 0)), Now);

        Assert.Empty(problems);
    }

    [Fact]
    public void NeverRanJob_JustAfterProcessStart_ProducesNoProblem()
    {
        // Все семь заданий стартуют вместе при старте процесса; сторож может собрать первый
        // снимок раньше, чем задание успеет отработать хотя бы раз — это прогрев, не авария.
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(processStartedAtUtc: Now.AddSeconds(-1),
                new JobState(PlatformJobNames.ScheduledReports, TimeSpan.FromHours(1), null, 0)), Now);

        Assert.Empty(problems);
    }

    [Fact]
    public void NeverRanJob_PastOwnWindowSinceProcessStart_IsOverdue()
    {
        // Прогрев кончился (окно ScheduledReports — 3 часа), а прогонов всё ещё нет: задание
        // либо отключили, либо оно падает на самом старте. Это обязано быть видно.
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(processStartedAtUtc: Now.AddHours(-4),
                new JobState(PlatformJobNames.ScheduledReports, TimeSpan.FromHours(1), null, 0)), Now);

        Assert.Equal(PlatformIncidentKindNames.JobOverdue, Assert.Single(problems).Kind);
    }

    [Fact]
    public void ThreeConsecutiveFailures_ProduceFailingProblem()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), Now.AddMinutes(-5), 3)), Now);

        Assert.Equal(PlatformIncidentKindNames.JobFailing, Assert.Single(problems).Kind);
    }

    [Fact]
    public void NeverSucceededJob_FailingFromTheStart_StillProducesFailingProblem()
    {
        // Прогрев гасит job_overdue, но не job_failing: задание, которое падает сразу же на
        // старте процесса (ни одного успеха, три провала подряд), обязано быть видно немедленно —
        // это не "ещё не тикнуло", это реальная поломка.
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(processStartedAtUtc: Now.AddSeconds(-1),
                new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), null, 3)), Now);

        Assert.Equal(PlatformIncidentKindNames.JobFailing, Assert.Single(problems).Kind);
    }

    [Fact]
    public void TwoConsecutiveFailures_ProduceNoProblem()
    {
        // Порог именно порог (>=3), а не «любой провал» — иначе единичный сбой уже будил бы дежурного.
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), Now.AddMinutes(-5), 2)), Now);

        Assert.Empty(problems);
    }

    [Fact]
    public void StuckNotificationQueue_IsCritical()
    {
        var problems = PlatformHealthRules.Evaluate(new HealthSnapshot([], 2, 0, 0, 0, Now), Now);

        var problem = Assert.Single(problems);
        Assert.Equal(PlatformIncidentKindNames.NotificationQueueStuck, problem.Kind);
        Assert.Equal(PlatformIncidentSeverityNames.Critical, problem.Severity);
    }

    [Fact]
    public void StuckBillingOutbox_IsCritical()
    {
        var problems = PlatformHealthRules.Evaluate(new HealthSnapshot([], 0, 0, 0, 3, Now), Now);

        var problem = Assert.Single(problems);
        Assert.Equal(PlatformIncidentKindNames.BillingOutboxStuck, problem.Kind);
        Assert.Equal(PlatformIncidentSeverityNames.Critical, problem.Severity);
    }
}
