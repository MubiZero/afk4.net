using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformIncidentServiceTests
{
    [Fact]
    public async Task SecondDetection_DoesNotOpenSecondIncident()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var first = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{\"minutes\":200}", CancellationToken.None);
        var second = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{\"minutes\":260}", CancellationToken.None);

        Assert.True(first.IsNew);
        Assert.False(second.IsNew);
        Assert.Equal(1, await db.PlatformIncidents.CountAsync(incident => incident.ResolvedAtUtc == null));
    }

    [Fact]
    public async Task ResolveMissing_ClosesIncidentsNoLongerDetected()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();

        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobFailing, "job_failing:billing_outbox",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);
        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobFailing, "job_failing:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);

        var resolved = await service.ResolveMissingAsync(["job_failing:daily_summary"], CancellationToken.None);

        Assert.Equal("job_failing:billing_outbox", Assert.Single(resolved).DedupKey);
        var open = await service.ListOpenAsync(CancellationToken.None);
        Assert.Equal("job_failing:daily_summary", Assert.Single(open).DedupKey);
    }

    [Fact]
    public async Task ReopeningAfterResolve_CreatesNewIncident()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();

        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:auto_protection",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);
        await service.ResolveMissingAsync([], CancellationToken.None);
        var reopened = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:auto_protection",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);

        Assert.True(reopened.IsNew);
    }
}
