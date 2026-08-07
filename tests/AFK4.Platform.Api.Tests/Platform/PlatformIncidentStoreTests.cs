using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformIncidentStoreTests
{
    [Fact]
    public async Task JobRun_And_Incident_RoundTrip()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformJobRuns.Add(new PlatformJobRunEntity
        {
            PlatformJobRunId = Guid.NewGuid(),
            JobName = "invoice_generation",
            StartedAtUtc = now.AddSeconds(-2),
            FinishedAtUtc = now,
            Outcome = PlatformJobOutcomeNames.Succeeded,
            ItemsProcessed = 3
        });
        db.PlatformIncidents.Add(new PlatformIncidentEntity
        {
            PlatformIncidentId = Guid.NewGuid(),
            Kind = PlatformIncidentKindNames.JobOverdue,
            DedupKey = "job_overdue:invoice_generation",
            Severity = PlatformIncidentSeverityNames.Critical,
            DetailsJson = "{\"minutes\":180}",
            OpenedAtUtc = now,
            LastSeenAtUtc = now
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PlatformJobRuns.CountAsync());
        Assert.Equal(1, await db.PlatformIncidents.CountAsync(incident => incident.ResolvedAtUtc == null));
    }
}
