using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformHealthRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    private static HealthSnapshot Snapshot(params JobState[] jobs) => new(jobs, 0, 0, 0, 0);

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
    public void NeverRanJob_IsOverdue()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.ScheduledReports, TimeSpan.FromHours(1), null, 0)), Now);

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
    public void StuckNotificationQueue_IsCritical()
    {
        var problems = PlatformHealthRules.Evaluate(new HealthSnapshot([], 2, 0, 0, 0), Now);

        var problem = Assert.Single(problems);
        Assert.Equal(PlatformIncidentKindNames.NotificationQueueStuck, problem.Kind);
        Assert.Equal(PlatformIncidentSeverityNames.Critical, problem.Severity);
    }

    [Fact]
    public void StuckBillingOutbox_IsCritical()
    {
        var problems = PlatformHealthRules.Evaluate(new HealthSnapshot([], 0, 0, 0, 3), Now);

        var problem = Assert.Single(problems);
        Assert.Equal(PlatformIncidentKindNames.BillingOutboxStuck, problem.Kind);
        Assert.Equal(PlatformIncidentSeverityNames.Critical, problem.Severity);
    }
}
